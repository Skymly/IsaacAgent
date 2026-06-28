using System;
using System.IO;
using System.Linq;

using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[UnsetVisualStudioEnvironmentVariables]
sealed class Build : NukeBuild
{
    /// <summary>
    ///   Build configuration: Debug locally, Release on CI.
    /// </summary>
    [Parameter("Build configuration (Debug/Release)")]
    readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    /// <summary>
    ///   Version override. When set, passed to publish as -p:Version.
    ///   When null, MinVer derives the version from the latest git tag.
    /// </summary>
    [Parameter("Version override (defaults to MinVer git-tag-based version)")]
    readonly string? Version = Environment.GetEnvironmentVariable("VERSION");

    /// <summary>
    ///   Target runtime for self-contained publish (default: win-x64).
    /// </summary>
    [Parameter("Target runtime identifier for publish (e.g. win-x64, linux-x64)")]
    readonly string Runtime = "win-x64";

    AbsolutePath Root => RootDirectory;
    AbsolutePath SolutionFile => Root / "IsaacAgent.sln";
    AbsolutePath AppProject => Root / "src" / "IsaacAgent.App" / "IsaacAgent.App.csproj";
    AbsolutePath TestResultsDirectory => Root / "TestResults";
    AbsolutePath ArtifactsDirectory => Root / "artifacts";
    AbsolutePath PublishDirectory => ArtifactsDirectory / "publish" / Runtime;

    static readonly string[] TestProjectRelativePaths =
    [
        "tests/IsaacAgent.Tests/IsaacAgent.Tests.csproj",
    ];

    public static int Main() => Execute<Build>(x => x.Ci);

    Target Clean => _ => _
        .Executes(() =>
        {
            if (TestResultsDirectory.DirectoryExists())
            {
                TestResultsDirectory.DeleteDirectory();
            }

            TestResultsDirectory.CreateDirectory();

            if (ArtifactsDirectory.DirectoryExists())
            {
                ArtifactsDirectory.DeleteDirectory();
            }
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(SolutionFile));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(SolutionFile)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target UnitTest => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            foreach (string relativePath in TestProjectRelativePaths)
            {
                AbsolutePath projectFile = Root / relativePath;
                if (!projectFile.FileExists())
                {
                    throw new InvalidOperationException($"Test project not found: {projectFile}");
                }

                DotNetTest(s => s
                    .SetProjectFile(projectFile)
                    .SetConfiguration(Configuration)
                    .SetNoBuild(true)
                    .SetResultsDirectory(TestResultsDirectory)
                    .SetLoggers("trx;LogFileName=" + projectFile.NameWithoutExtension + ".trx")
                    .SetDataCollector("XPlat Code Coverage"));
            }
        });

    /// <summary>
    ///   Cross-platform library test target. Builds and tests only the test
    ///   project (which transitively builds Core/LLM/Tools/Agent/Rag but NOT
    ///   the Windows-only App project). Safe to run on Linux/macOS.
    /// </summary>
    Target UnitTestLib => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            foreach (string relativePath in TestProjectRelativePaths)
            {
                AbsolutePath projectFile = Root / relativePath;
                if (!projectFile.FileExists())
                {
                    throw new InvalidOperationException($"Test project not found: {projectFile}");
                }

                // No --no-build: dotnet test will build the test project and
                // its library dependencies automatically, skipping the App project.
                DotNetTest(s => s
                    .SetProjectFile(projectFile)
                    .SetConfiguration(Configuration)
                    .SetResultsDirectory(TestResultsDirectory)
                    .SetLoggers("trx;LogFileName=" + projectFile.NameWithoutExtension + ".trx")
                    .SetDataCollector("XPlat Code Coverage"));
            }
        });

    /// <summary>
    ///   Convenience alias for UnitTest.
    /// </summary>
    Target Test => _ => _
        .DependsOn(UnitTest);

    Target Format => _ => _
        .Executes(() =>
        {
            DotNet($"format \"{SolutionFile}\" --verify-no-changes --verbosity diagnostic");
        });

    Target FormatFix => _ => _
        .Executes(() =>
        {
            DotNet($"format \"{SolutionFile}\" --verbosity normal");
        });

    /// <summary>
    ///   Publishes a self-contained single-file executable.
    ///   Output: artifacts/publish/{Runtime}/
    /// </summary>
    Target Publish => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            PublishDirectory.CreateOrCleanDirectory();

            DotNetPublish(s =>
            {
                s = s
                    .SetProject(AppProject)
                    .SetConfiguration(Configuration)
                    .SetRuntime(Runtime)
                    .SetSelfContained(true)
                    .SetOutput(PublishDirectory)
                    .SetProperty("PublishSingleFile", "true")
                    .SetProperty("IncludeNativeLibrariesForSelfExtract", "true");

                if (!string.IsNullOrWhiteSpace(Version))
                {
                    s = s.SetProperty("Version", Version);
                }

                return s;
            });
        });

    /// <summary>
    ///   Verifies the published executable exists and has a reasonable size
    ///   for a self-contained deployment (ONNX runtime + Avalonia natives
    ///   typically produce >50 MB).
    /// </summary>
    Target PublishVerify => _ => _
        .DependsOn(Publish)
        .Executes(() =>
        {
            AbsolutePath exe = PublishDirectory / "IsaacAgent.exe";
            AbsolutePath dll = PublishDirectory / "IsaacAgent.dll";

            // On non-Windows runtimes the output is .dll; on Windows it's .exe.
            AbsolutePath entryPoint = exe.FileExists() ? exe : dll;
            Assert.FileExists(entryPoint,
                $"Published entry point not found. Expected {exe} or {dll} in {PublishDirectory}");

            var sizeMb = new FileInfo(entryPoint).Length / (1024.0 * 1024.0);
            Assert.True(sizeMb > 50,
                $"Published executable is only {sizeMb:F1} MB — expected >50 MB for a self-contained deployment. " +
                "Native libraries (ONNX runtime, Avalonia) may be missing.");

            Console.WriteLine($"Publish verified: {entryPoint.Name} ({sizeMb:F1} MB) at {PublishDirectory}");
        });

    /// <summary>
    ///   CI entry point: Clean → Restore → Compile → UnitTest.
    /// </summary>
    Target Ci => _ => _
        .DependsOn(UnitTest)
        .Executes(() =>
        {
            Console.WriteLine("CI build completed successfully.");
        });

    /// <summary>
    ///   Cross-platform CI entry point: Clean → UnitTestLib (library tests
    ///   only, no App project). Safe for Linux/macOS runners.
    /// </summary>
    Target CiLib => _ => _
        .DependsOn(UnitTestLib)
        .Executes(() =>
        {
            Console.WriteLine("Cross-platform library CI completed successfully.");
        });

    /// <summary>
    ///   Full local/CI verification: Format + Ci.
    /// </summary>
    Target CiAll => _ => _
        .DependsOn(Format)
        .DependsOn(Ci)
        .Executes(() =>
        {
            Console.WriteLine("Full verification (format + CI) completed successfully.");
        });

    /// <summary>
    ///   Full release pipeline: CiAll → Publish → PublishVerify.
    ///   Run on tag pushes (v*) or manually with --target Release.
    /// </summary>
    Target Release => _ => _
        .DependsOn(CiAll)
        .DependsOn(PublishVerify)
        .Executes(() =>
        {
            Console.WriteLine("Release pipeline completed successfully.");
        });
}

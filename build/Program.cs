using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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
    ///   Target runtime for self-contained publish. Official support is
    ///   Windows-only (ADR-003); default and expected value is win-x64.
    /// </summary>
    [Parameter("Target runtime identifier for publish (win-x64)")]
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
    ///   Verifies the published executable: size, side-by-side ONNX assets,
    ///   and a headless <c>--verify-onnx</c> run from an EXE-only folder
    ///   (GitHub Release ships only the exe).
    /// </summary>
    Target PublishVerify => _ => _
        .DependsOn(Publish)
        .Executes(() =>
        {
            AbsolutePath exe = PublishDirectory / "IsaacAgent.exe";

            Assert.FileExists(exe,
                $"Published entry point not found. Expected {exe} in {PublishDirectory}");

            var sizeMb = new FileInfo(exe).Length / (1024.0 * 1024.0);
            Assert.True(sizeMb > 100,
                $"Published executable is only {sizeMb:F1} MB — expected >100 MB for a self-contained " +
                "deployment with the embedded ONNX model (~86 MB) plus Avalonia/ONNX Runtime natives.");

            AbsolutePath sideModel = PublishDirectory / "onnx" / "model.onnx";
            AbsolutePath sideVocab = PublishDirectory / "onnx" / "vocab.txt";
            Assert.FileExists(sideModel,
                $"Side-by-side ONNX model missing after publish: {sideModel}");
            Assert.FileExists(sideVocab,
                $"Side-by-side ONNX vocab missing after publish: {sideVocab}");

            var modelMb = new FileInfo(sideModel).Length / (1024.0 * 1024.0);
            Assert.True(modelMb > 80,
                $"Side-by-side ONNX model is only {modelMb:F1} MB — expected >80 MB for all-MiniLM-L6-v2.");

            AbsolutePath verifyDir = ArtifactsDirectory / "verify-onnx-exe";
            verifyDir.CreateOrCleanDirectory();
            AbsolutePath isolatedExe = verifyDir / "IsaacAgent.exe";
            File.Copy(exe, isolatedExe);

            // On GitHub Actions runners, force AppData extraction by clearing
            // any prior onnx cache. Local runs keep existing AppData assets.
            if (string.Equals(
                    Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                var appDataOnnx = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IsaacAgent",
                    "onnx");
                if (Directory.Exists(appDataOnnx))
                    Directory.Delete(appDataOnnx, recursive: true);
            }

            var psi = new ProcessStartInfo
            {
                FileName = isolatedExe,
                Arguments = "--verify-onnx",
                WorkingDirectory = verifyDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start {isolatedExe}");

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            if (!proc.WaitForExit(120_000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                throw new InvalidOperationException(
                    $"--verify-onnx timed out after 120s.\nstdout:\n{stdout}\nstderr:\n{stderr}");
            }

            // Drain async readers
            proc.WaitForExit();

            Assert.True(proc.ExitCode == 0,
                $"--verify-onnx failed with exit code {proc.ExitCode}.\nstdout:\n{stdout}\nstderr:\n{stderr}");

            Console.WriteLine($"Publish verified: {exe.Name} ({sizeMb:F1} MB) at {PublishDirectory}");
            Console.WriteLine($"ONNX side-by-side model: {modelMb:F1} MB; --verify-onnx OK from EXE-only dir.");
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

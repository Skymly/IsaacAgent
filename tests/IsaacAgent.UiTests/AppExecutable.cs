namespace IsaacAgent.UiTests;

/// <summary>
/// Resolves the Release <c>IsaacAgent.exe</c> under test (build output, not Publish).
/// Override with <c>ISAACAGENT_APP_EXE</c> when needed.
/// </summary>
internal static class AppExecutable
{
    public const string EnvVarName = "ISAACAGENT_APP_EXE";

    public static string ResolveReleasePath()
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            var full = Path.GetFullPath(fromEnv);
            if (!File.Exists(full))
            {
                throw new FileNotFoundException(
                    $"ISAACAGENT_APP_EXE points to a missing file: {full}", full);
            }

            return full;
        }

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "Could not locate repo root (missing IsaacAgent.sln) from " +
                AppContext.BaseDirectory);

        var exe = Path.Combine(
            repoRoot,
            "src",
            "IsaacAgent.App",
            "bin",
            "Release",
            "net8.0",
            "IsaacAgent.exe");

        if (!File.Exists(exe))
        {
            throw new FileNotFoundException(
                "Release App build not found. Build with: " +
                "dotnet build src/IsaacAgent.App/IsaacAgent.App.csproj -c Release. " +
                $"Expected: {exe}",
                exe);
        }

        return exe;
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "IsaacAgent.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }
}

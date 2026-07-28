namespace IsaacAgent.Core.PathSafety;

/// <summary>
/// Project-sandbox path policy: resolve relative paths under a project root,
/// sibling-prefix-safe containment checks, and the Isaac default log.txt
/// absolute-path whitelist. Single Core implementation for Tools / Agent / Rag / App.
/// </summary>
public static class ProjectPathSafety
{
    /// <summary>
    /// Normalizes a relative path by collapsing sequences of 3+ dots
    /// (e.g. "....") into ".." to defeat double-encoded traversal attempts
    /// like "....//target/evil.lua" that <see cref="Path.GetFullPath"/>
    /// treats as a literal directory name rather than a parent reference.
    /// </summary>
    private static string NormalizeRelativePath(string relPath)
    {
        var normalized = relPath.Replace('/', Path.DirectorySeparatorChar)
                                .Replace('\\', Path.DirectorySeparatorChar);
        var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.None);
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length >= 3 && segments[i].All(c => c == '.'))
                segments[i] = "..";
        }
        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    /// <summary>
    /// Resolves <paramref name="relPath"/> against <paramref name="projectDir"/>
    /// after normalizing double-encoded traversal patterns, then verifies
    /// the resulting full path stays within the project directory.
    /// </summary>
    public static (string FullPath, bool IsSafe) Resolve(string projectDir, string relPath)
    {
        var projectRoot = Path.GetFullPath(projectDir);
        var normalizedRel = NormalizeRelativePath(relPath);
        var fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalizedRel));
        return (fullPath, IsWithinProject(fullPath, projectRoot));
    }

    /// <summary>
    /// Returns true when <paramref name="fullPath"/> is the project root itself
    /// or a path strictly under it (sibling-prefix safe via trailing separator).
    /// </summary>
    public static bool IsWithinProject(string fullPath, string projectDir)
    {
        var projectRoot = projectDir.EndsWith(Path.DirectorySeparatorChar)
            ? projectDir
            : projectDir + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, projectDir, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Canonical absolute path for the default Isaac Repentance <c>log.txt</c>
    /// under Documents/My Games (existence is not checked).
    /// </summary>
    public static string GetDefaultIsaacLogPath()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "My Games", "Binding of Isaac Repentance", "log.txt");
    }

    /// <summary>
    /// Absolute paths are allowed only when they equal the default Isaac log path
    /// (case-insensitive). Relative project paths use <see cref="Resolve"/> instead.
    /// </summary>
    public static bool IsAllowedAbsoluteLogPath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return false;
        return string.Equals(
            Path.GetFullPath(absolutePath),
            Path.GetFullPath(GetDefaultIsaacLogPath()),
            StringComparison.OrdinalIgnoreCase);
    }
}

namespace IsaacAgent.Agent.Engine;

/// <summary>
/// Project-sandbox path resolution for Before-image capture (mirrors Tools
/// <c>FileToolPathSafety</c> without crossing module internals).
/// </summary>
internal static class ProjectPathSafety
{
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

    public static (string FullPath, bool IsSafe) Resolve(string projectDir, string relPath)
    {
        var projectRoot = Path.GetFullPath(projectDir);
        var normalizedRel = NormalizeRelativePath(relPath);
        var fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalizedRel));
        return (fullPath, IsWithinProject(fullPath, projectRoot));
    }

    public static bool IsWithinProject(string fullPath, string projectDir)
    {
        var projectRoot = projectDir.EndsWith(Path.DirectorySeparatorChar)
            ? projectDir
            : projectDir + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, projectDir, StringComparison.OrdinalIgnoreCase);
    }

    public static string ToRelativeKey(string projectDir, string fullPath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(projectDir), fullPath);
        return relative.Replace('\\', '/');
    }
}

namespace IsaacAgent.Agent.Engine;

/// <summary>
/// Checkpoint Before-image key normalization (forward-slash relative keys).
/// Sandbox resolve / containment live in Core <c>ProjectPathSafety</c>.
/// </summary>
internal static class CheckpointRelativePaths
{
    public static string ToRelativeKey(string projectDir, string fullPath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(projectDir), fullPath);
        return relative.Replace('\\', '/');
    }
}

namespace IsaacAgent.App.Services;

/// <summary>
/// Parses optional desktop launch flags (e.g. <c>--project</c> for UI E2E).
/// </summary>
internal static class AppLaunchArgs
{
    /// <summary>
    /// Returns the path after <c>--project</c>, or null when the flag is omitted,
    /// has no value, or the next token looks like another flag.
    /// Does not validate that the path exists.
    /// </summary>
    public static string? TryGetProjectPath(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], "--project", StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= args.Count)
                return null;

            var value = args[i + 1];
            if (value.StartsWith('-'))
                return null;

            return value;
        }

        return null;
    }
}

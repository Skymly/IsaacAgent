using System.Reflection;

namespace IsaacAgent.Rag.Chunking;

/// <summary>
/// Shared helper methods used across multiple chunker implementations.
/// </summary>
internal static class ChunkerHelpers
{
    /// <summary>
    /// Returns manifest resource names from the given assembly that start with
    /// <paramref name="resourcePrefix"/> and end with <c>.md</c>, ordered by name.
    /// </summary>
    public static IOrderedEnumerable<string> GetMarkdownResourceNames(Assembly assembly, string resourcePrefix)
    {
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal))
            .OrderBy(n => n);
    }

    /// <summary>
    /// Parses simple YAML frontmatter (key: value pairs and multi-line lists)
    /// into the provided metadata dictionary.
    /// </summary>
    public static void ParseFrontMatter(string frontMatter, Dictionary<string, string> metadata)
    {
        var lines = frontMatter.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();

            // Multi-line YAML list: key:\n  - value
            if (string.IsNullOrEmpty(value) && i + 1 < lines.Length)
            {
                var nextLine = lines[i + 1].TrimStart();
                if (nextLine.StartsWith('-'))
                {
                    value = nextLine[1..].Trim();
                    i++;
                }
            }

            if (!string.IsNullOrEmpty(value))
                metadata[key] = value;
        }
    }
}

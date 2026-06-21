using System.Reflection;
using System.Text.RegularExpressions;
using IsaacAgent.Core.Models;

namespace IsaacAgent.Rag.Chunking;

/// <summary>
/// Chunks MkDocs Material markdown files (IsaacDocs / REPENTOGON format).
/// Reads from embedded resources or filesystem. Parses YAML frontmatter
/// (tags → category), splits by ### headings, and cleans MkDocs-specific
/// syntax (admonitions, attribute annotations) for better embeddings.
/// </summary>
public static class MkDocsChunker
{
    private static readonly Regex FrontMatterRegex = new(@"^---\s*\n(.*?)\n---\s*\n", RegexOptions.Singleline);
    private static readonly Regex HeadingRegex = new(@"^(#{1,3})\s+(.+)$", RegexOptions.Multiline);
    private static readonly Regex AdmonitionRegex = new(@"^\?\?\?[-+]?\s+(\w+)\s+[""'](.+?)[""']\s*$", RegexOptions.Multiline);
    private static readonly Regex AttrAnnotationRegex = new(@"\{:\s+[^}]+\}", RegexOptions.Compiled);
    private static readonly Regex H1TitleRegex = new(@"^#\s+""?([^""]+)""?\s*$", RegexOptions.Multiline);

    private static readonly Dictionary<string, string> TagToCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Enum"] = "enum",
        ["Class"] = "class",
        ["File"] = "xml",
        ["Globals"] = "global",
        ["Global"] = "global",
        ["Tutorial"] = "tutorial",
        ["FAQ"] = "faq",
        ["Tools"] = "tool",
    };

    /// <summary>
    /// Chunk all markdown files from embedded resources under the given prefix.
    /// e.g. "IsaacAgent.Rag.Resources.docs.vanilla" for vanilla docs.
    /// </summary>
    public static List<KnowledgeChunk> ChunkFromEmbeddedResources(Assembly assembly, string resourcePrefix, string source)
    {
        var chunks = new List<KnowledgeChunk>();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal))
            .OrderBy(n => n);

        foreach (var name in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            var relativeName = name[(resourcePrefix.Length + 1)..];
            chunks.AddRange(ChunkMarkdown(content, relativeName, source));
        }
        return chunks;
    }

    /// <summary>
    /// Chunk all markdown files from a filesystem directory.
    /// </summary>
    public static List<KnowledgeChunk> ChunkFromDirectory(string dirPath, string source)
    {
        var chunks = new List<KnowledgeChunk>();
        if (!Directory.Exists(dirPath)) return chunks;

        foreach (var file in Directory.EnumerateFiles(dirPath, "*.md", SearchOption.AllDirectories))
        {
            var relativeName = Path.GetRelativePath(dirPath, file).Replace('\\', '/');
            var content = File.ReadAllText(file);
            chunks.AddRange(ChunkMarkdown(content, relativeName, source));
        }
        return chunks;
    }

    public static List<KnowledgeChunk> ChunkMarkdown(string content, string fileName, string source)
    {
        var metadata = new Dictionary<string, string>();
        var body = content;

        var fmMatch = FrontMatterRegex.Match(content);
        if (fmMatch.Success)
        {
            body = content[(fmMatch.Index + fmMatch.Length)..];
            ParseFrontMatter(fmMatch.Groups[1].Value, metadata);
        }

        var category = DetermineCategory(metadata, fileName);
        var docTitle = ExtractDocTitle(body, fileName, metadata);

        body = CleanMkDocsSyntax(body);

        var sections = SplitByHeadings(body);
        if (sections.Count == 0 || (sections.Count == 1 && sections[0].Content.Length < 100))
        {
            var singleContent = body.Trim();
            if (string.IsNullOrWhiteSpace(singleContent)) return [];
            return [CreateChunk($"{source}:{fileName}", source, category, docTitle, singleContent, metadata)];
        }

        var chunks = new List<KnowledgeChunk>();
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (string.IsNullOrWhiteSpace(section.Content)) continue;
            var title = section.Heading is null ? docTitle : $"{docTitle} — {section.Heading}";
            chunks.Add(CreateChunk($"{source}:{fileName}:{i}", source, category, title, section.Content.Trim(), metadata));
        }
        return chunks;
    }

    private static void ParseFrontMatter(string frontMatter, Dictionary<string, string> metadata)
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

    private static string DetermineCategory(Dictionary<string, string> metadata, string fileName)
    {
        if (metadata.TryGetValue("tags", out var tags))
        {
            if (TagToCategory.TryGetValue(tags, out var category))
                return category;
        }

        if (fileName.Contains("enums/", StringComparison.OrdinalIgnoreCase) || fileName.Contains("enum", StringComparison.OrdinalIgnoreCase))
            return "enum";
        if (fileName.Contains("xml/", StringComparison.OrdinalIgnoreCase))
            return "xml";
        if (fileName.Contains("tutorial", StringComparison.OrdinalIgnoreCase))
            return "tutorial";
        if (fileName.Contains("faq", StringComparison.OrdinalIgnoreCase))
            return "faq";
        return "doc";
    }

    private static string ExtractDocTitle(string body, string fileName, Dictionary<string, string> metadata)
    {
        var h1Match = H1TitleRegex.Match(body);
        if (h1Match.Success)
            return h1Match.Groups[1].Value.Trim();

        return Path.GetFileNameWithoutExtension(fileName);
    }

    /// <summary>
    /// Clean MkDocs Material-specific syntax that would pollute embeddings:
    /// - Remove {: .copyable } and similar attribute annotations
    /// - Convert ??? admonitions to plain headers
    /// - Remove MkDocs badge/link annotations
    /// </summary>
    private static string CleanMkDocsSyntax(string body)
    {
        body = AttrAnnotationRegex.Replace(body, "");
        body = AdmonitionRegex.Replace(body, match => $"**{match.Groups[2].Value}**");
        return body;
    }

    private static List<(string? Heading, string Content)> SplitByHeadings(string body)
    {
        var sections = new List<(string? Heading, string Content)>();
        var matches = HeadingRegex.Matches(body);
        if (matches.Count == 0)
        {
            sections.Add((null, body));
            return sections;
        }

        if (matches[0].Index > 0)
            sections.Add((null, body[..matches[0].Index]));

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var heading = match.Groups[2].Value.Trim();
            var start = match.Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : body.Length;
            sections.Add((heading, body[start..end]));
        }
        return sections;
    }

    private static KnowledgeChunk CreateChunk(string id, string source, string category, string title, string content, Dictionary<string, string> metadata)
    {
        return new KnowledgeChunk
        {
            Id = id,
            Source = source,
            Category = category,
            Title = title,
            Content = content,
            Metadata = metadata
        };
    }
}

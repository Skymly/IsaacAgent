using System.IO;
using System.Text.RegularExpressions;
using IsaacAgent.Core.Models;

namespace IsaacAgent.Rag.Chunking;

public static class MarkdownChunker
{
    private static readonly Regex FrontMatterRegex = new(@"^---\s*\n(.*?)\n---\s*\n", RegexOptions.Singleline);
    private static readonly Regex HeadingRegex = new(@"^(#{1,3})\s+(.+)$", RegexOptions.Multiline);

    /// <summary>Minimum section size in characters — smaller sections are emitted as a single chunk.</summary>
    private const int MinSectionSize = 200;

    public static List<KnowledgeChunk> ChunkDirectory(string dirPath, string source = "example")
    {
        var chunks = new List<KnowledgeChunk>();
        if (!Directory.Exists(dirPath)) return chunks;

        foreach (var file in Directory.EnumerateFiles(dirPath, "*.md", SearchOption.AllDirectories))
        {
            var relativeName = Path.GetRelativePath(dirPath, file);
            var content = File.ReadAllText(file);
            chunks.AddRange(ChunkMarkdown(content, relativeName, source));
        }
        return chunks;
    }

    public static List<KnowledgeChunk> ChunkMarkdown(string content, string fileName, string source = "example")
    {
        var metadata = new Dictionary<string, string>();
        var body = content;

        var fmMatch = FrontMatterRegex.Match(content);
        if (fmMatch.Success)
        {
            body = content[(fmMatch.Index + fmMatch.Length)..];
            ChunkerHelpers.ParseFrontMatter(fmMatch.Groups[1].Value, metadata);
        }

        var title = metadata.TryGetValue("title", out var t) ? t : fileName;
        var category = metadata.TryGetValue("category", out var c) ? c : "example";
        if (metadata.TryGetValue("tags", out var tags))
            metadata["tags"] = tags;

        var sections = SplitByHeadings(body);
        if (sections.Count == 0 || (sections.Count == 1 && sections[0].Content.Length < MinSectionSize))
        {
            return [new KnowledgeChunk
            {
                Id = $"{source}:{fileName}",
                Source = source,
                Category = category,
                Title = title,
                Content = body.Trim(),
                Metadata = new Dictionary<string, string>(metadata)
            }];
        }

        var chunks = new List<KnowledgeChunk>();
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (string.IsNullOrWhiteSpace(section.Content)) continue;
            chunks.Add(new KnowledgeChunk
            {
                Id = $"{source}:{fileName}:{i}",
                Source = source,
                Category = category,
                Title = section.Heading is null ? title : $"{title} — {section.Heading}",
                Content = section.Content.Trim(),
                Metadata = new Dictionary<string, string>(metadata)
            });
        }
        return chunks;
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
}

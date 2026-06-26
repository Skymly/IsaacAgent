using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using IsaacAgent.Core.Models;

namespace IsaacAgent.Rag.Chunking;

/// <summary>
/// Improved markdown chunker with overlap windows, code-block protection,
/// and max-chunk-size enforcement. Prevents context loss at chunk boundaries
/// and avoids splitting inside fenced code blocks.
/// </summary>
public static class SmartMarkdownChunker
{
    private static readonly Regex FrontMatterRegex = new(@"^---\s*\n(.*?)\n---\s*\n", RegexOptions.Singleline);
    private static readonly Regex HeadingRegex = new(@"^(#{1,3})\s+(.+)$", RegexOptions.Multiline);
    private static readonly Regex CodeBlockRegex = new(@"^```", RegexOptions.Multiline);

    /// <summary>Maximum chunk size in characters (≈512 tokens for embedding).</summary>
    private const int MaxChunkSize = 2000;

    /// <summary>Overlap between adjacent chunks to preserve context.</summary>
    private const int OverlapChars = 200;

    /// <summary>Minimum chunk size — smaller sections are merged with neighbors.</summary>
    private const int MinChunkSize = 100;

    /// <summary>Minimum distance from start before snapping a split point to a line boundary.</summary>
    private const int MinSplitDistance = 100;

    public static List<KnowledgeChunk> ChunkDirectory(string dirPath, string source = "example")
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

    /// <summary>
    /// Chunk all markdown files from embedded resources under the given prefix.
    /// </summary>
    public static List<KnowledgeChunk> ChunkFromEmbeddedResources(Assembly assembly, string resourcePrefix, string source)
    {
        var chunks = new List<KnowledgeChunk>();
        var resourceNames = ChunkerHelpers.GetMarkdownResourceNames(assembly, resourcePrefix);

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
        metadata.TryAdd("file", fileName);

        // Split by headings, respecting code blocks
        var sections = SplitByHeadingsSafe(body);

        // Merge tiny sections into neighbors
        sections = MergeSmallSections(sections);

        var chunks = new List<KnowledgeChunk>();
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (string.IsNullOrWhiteSpace(section.Content)) continue;

            var sectionTitle = section.Heading is null ? title : $"{title} — {section.Heading}";

            // If section is small enough, emit as single chunk
            if (section.Content.Length <= MaxChunkSize)
            {
                chunks.Add(CreateChunk($"{source}:{fileName}:{i}", source, category, sectionTitle, section.Content.Trim(), metadata));
                continue;
            }

            // Split large sections with overlap, preserving code blocks
            var subChunks = SplitWithOverlap(section.Content, MaxChunkSize, OverlapChars);
            for (var j = 0; j < subChunks.Count; j++)
            {
                var subTitle = subChunks.Count > 1 ? $"{sectionTitle} (part {j + 1}/{subChunks.Count})" : sectionTitle;
                chunks.Add(CreateChunk($"{source}:{fileName}:{i}:{j}", source, category, subTitle, subChunks[j].Trim(), metadata));
            }
        }

        return chunks;
    }

    /// <summary>
    /// Split by headings but never split inside a fenced code block.
    /// </summary>
    private static List<(string? Heading, string Content)> SplitByHeadingsSafe(string body)
    {
        var sections = new List<(string? Heading, string Content)>();
        var lines = body.Split('\n');
        var inCodeBlock = false;
        string? currentHeading = null;
        var sectionLines = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Track code block state
            if (CodeBlockRegex.IsMatch(line))
                inCodeBlock = !inCodeBlock;

            // Only split on headings when NOT inside a code block
            if (!inCodeBlock)
            {
                var headingMatch = HeadingRegex.Match(line);
                if (headingMatch.Success && sectionLines.Count > 0)
                {
                    sections.Add((currentHeading, string.Join('\n', sectionLines)));
                    sectionLines.Clear();
                    currentHeading = headingMatch.Groups[2].Value.Trim();
                }
                else if (sectionLines.Count == 0 && headingMatch.Success)
                {
                    currentHeading = headingMatch.Groups[2].Value.Trim();
                }
            }

            sectionLines.Add(line);
        }

        if (sectionLines.Count > 0)
            sections.Add((currentHeading, string.Join('\n', sectionLines)));

        return sections;
    }

    /// <summary>
    /// Merge sections smaller than MinChunkSize into the previous section.
    /// </summary>
    private static List<(string? Heading, string Content)> MergeSmallSections(List<(string? Heading, string Content)> sections)
    {
        if (sections.Count <= 1) return sections;

        var merged = new List<(string? Heading, string Content)>();
        foreach (var section in sections)
        {
            if (merged.Count > 0 && section.Content.Trim().Length < MinChunkSize)
            {
                var last = merged[^1];
                merged[^1] = (last.Heading, last.Content + "\n" + section.Content);
            }
            else
            {
                merged.Add(section);
            }
        }
        return merged;
    }

    /// <summary>
    /// Split text into chunks of maxChars with overlapChars overlap between adjacent chunks.
    /// Respects code block boundaries — never splits inside a fenced code block.
    /// </summary>
    private static List<string> SplitWithOverlap(string text, int maxChars, int overlapChars)
    {
        var chunks = new List<string>();
        if (text.Length <= maxChars)
        {
            chunks.Add(text);
            return chunks;
        }

        var pos = 0;
        while (pos < text.Length)
        {
            var end = Math.Min(pos + maxChars, text.Length);

            // Don't split inside a code block — find the next safe boundary
            end = FindSafeSplitPoint(text, pos, end);

            chunks.Add(text[pos..end]);

            if (end >= text.Length) break;
            pos = Math.Max(pos + 1, end - overlapChars);
        }

        return chunks;
    }

    /// <summary>
    /// Adjust the split point to avoid cutting inside a fenced code block.
    /// Walks backward from the proposed end to find a line that's not inside ``` fences.
    /// </summary>
    private static int FindSafeSplitPoint(string text, int start, int proposedEnd)
    {
        if (proposedEnd >= text.Length) return text.Length;

        // Count code fence toggles from start to proposedEnd
        var subText = text[start..proposedEnd];
        var fenceCount = CodeBlockRegex.Matches(subText).Count;

        // If odd number of fences, we're inside a code block — find the next fence
        if (fenceCount % 2 == 1)
        {
            var nextFence = CodeBlockRegex.Match(text, proposedEnd);
            if (nextFence.Success)
            {
                // Include the closing fence line
                var lineEnd = text.IndexOf('\n', nextFence.Index);
                return lineEnd >= 0 ? lineEnd : text.Length;
            }
            // No closing fence found — take the rest
            return text.Length;
        }

        // Snap to line boundary for cleaner chunks
        var searchEnd = Math.Min(proposedEnd, text.Length - 1);
        var lineBreak = text.LastIndexOf('\n', searchEnd);
        if (lineBreak > start + MinSplitDistance) return lineBreak + 1;

        return proposedEnd;
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
            Metadata = new Dictionary<string, string>(metadata)
        };
    }
}

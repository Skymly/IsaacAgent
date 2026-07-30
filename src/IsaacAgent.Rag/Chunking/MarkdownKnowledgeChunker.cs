using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using IsaacAgent.Core.Models;

namespace IsaacAgent.Rag.Chunking;

/// <summary>
/// Deep markdown chunking module for knowledge indexing: front matter, fence-safe
/// heading splits, overlap windows, and optional MkDocs cleanup. Callers pick behaviour
/// via <see cref="MarkdownChunkOptions"/> presets — not separate chunker types.
/// </summary>
public static class MarkdownKnowledgeChunker
{
    private static readonly Regex FrontMatterRegex = new(@"^---\s*\n(.*?)\n---\s*\n", RegexOptions.Singleline);
    private static readonly Regex HeadingRegex = new(@"^(#{1,3})\s+(.+)$", RegexOptions.Multiline);
    private static readonly Regex CodeBlockRegex = new(@"^```", RegexOptions.Multiline);
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
    /// Chunk all markdown files under <paramref name="dirPath"/> (recursive).
    /// Missing directories yield an empty list.
    /// </summary>
    public static List<KnowledgeChunk> ChunkDirectory(
        string dirPath,
        string source,
        MarkdownChunkOptions? options = null)
    {
        options ??= MarkdownChunkOptions.ForPatternsOrExamples;
        var chunks = new List<KnowledgeChunk>();
        if (!Directory.Exists(dirPath)) return chunks;

        foreach (var file in Directory.EnumerateFiles(dirPath, "*.md", SearchOption.AllDirectories))
        {
            var relativeName = Path.GetRelativePath(dirPath, file).Replace('\\', '/');
            var content = File.ReadAllText(file);
            chunks.AddRange(ChunkMarkdown(content, relativeName, source, options));
        }
        return chunks;
    }

    /// <summary>
    /// Chunk all embedded <c>.md</c> resources under <paramref name="resourcePrefix"/>.
    /// </summary>
    public static List<KnowledgeChunk> ChunkFromEmbeddedResources(
        Assembly assembly,
        string resourcePrefix,
        string source,
        MarkdownChunkOptions? options = null)
    {
        options ??= MarkdownChunkOptions.ForPatternsOrExamples;
        var chunks = new List<KnowledgeChunk>();
        var resourceNames = ChunkerHelpers.GetMarkdownResourceNames(assembly, resourcePrefix);

        foreach (var name in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            var relativeName = name[(resourcePrefix.Length + 1)..];
            chunks.AddRange(ChunkMarkdown(content, relativeName, source, options));
        }
        return chunks;
    }

    public static List<KnowledgeChunk> ChunkMarkdown(
        string content,
        string fileName,
        string source,
        MarkdownChunkOptions? options = null)
    {
        options ??= MarkdownChunkOptions.ForPatternsOrExamples;
        var metadata = new Dictionary<string, string>();
        var body = content;

        var fmMatch = FrontMatterRegex.Match(content);
        if (fmMatch.Success)
        {
            body = content[(fmMatch.Index + fmMatch.Length)..];
            ChunkerHelpers.ParseFrontMatter(fmMatch.Groups[1].Value, metadata);
        }

        var category = ResolveCategory(metadata, fileName, options);
        var docTitle = ResolveDocTitle(body, fileName, metadata, options);

        if (options.CleanMkDocsSyntax)
            body = CleanMkDocsSyntax(body);

        if (options.IncludeFileMetadata)
            metadata.TryAdd("file", fileName);

        var sections = SplitByHeadingsSafe(body);

        if (options.UseMkDocsCategoryResolution)
        {
            if (sections.Count == 0 || (sections.Count == 1 && sections[0].Content.Length < options.MinChunkSize))
            {
                var singleContent = body.Trim();
                if (string.IsNullOrWhiteSpace(singleContent)) return [];
                return [CreateChunk($"{source}:{fileName}", source, category, docTitle, singleContent, metadata)];
            }
        }
        else if (options.MergeSmallSections)
        {
            sections = MergeSmallSections(sections, options.MinChunkSize);
        }

        var chunks = new List<KnowledgeChunk>();
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (string.IsNullOrWhiteSpace(section.Content)) continue;

            var sectionTitle = section.Heading is null ? docTitle : $"{docTitle} — {section.Heading}";

            if (section.Content.Length <= options.MaxChunkSize)
            {
                chunks.Add(CreateChunk($"{source}:{fileName}:{i}", source, category, sectionTitle, section.Content.Trim(), metadata));
                continue;
            }

            var subChunks = SplitWithOverlap(
                section.Content, options.MaxChunkSize, options.OverlapChars, options.MinSplitDistance);
            for (var j = 0; j < subChunks.Count; j++)
            {
                var subTitle = subChunks.Count > 1 ? $"{sectionTitle} (part {j + 1}/{subChunks.Count})" : sectionTitle;
                chunks.Add(CreateChunk($"{source}:{fileName}:{i}:{j}", source, category, subTitle, subChunks[j].Trim(), metadata));
            }
        }

        return chunks;
    }

    private static string ResolveCategory(Dictionary<string, string> metadata, string fileName, MarkdownChunkOptions options)
    {
        if (options.UseMkDocsCategoryResolution)
            return DetermineMkDocsCategory(metadata, fileName);

        return metadata.TryGetValue("category", out var c) ? c : "example";
    }

    private static string ResolveDocTitle(string body, string fileName, Dictionary<string, string> metadata, MarkdownChunkOptions options)
    {
        if (options.PreferH1DocTitle)
        {
            var h1Match = H1TitleRegex.Match(body);
            if (h1Match.Success)
                return h1Match.Groups[1].Value.Trim();

            return Path.GetFileNameWithoutExtension(fileName);
        }

        return metadata.TryGetValue("title", out var t) ? t : fileName;
    }

    private static string DetermineMkDocsCategory(Dictionary<string, string> metadata, string fileName)
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

    private static string CleanMkDocsSyntax(string body)
    {
        body = AttrAnnotationRegex.Replace(body, "");
        body = AdmonitionRegex.Replace(body, match => $"**{match.Groups[2].Value}**");
        return body;
    }

    private static List<(string? Heading, string Content)> SplitByHeadingsSafe(string body)
    {
        var sections = new List<(string? Heading, string Content)>();
        var lines = body.Split('\n');
        var inCodeBlock = false;
        string? currentHeading = null;
        var sectionLines = new List<string>();

        foreach (var line in lines)
        {
            if (CodeBlockRegex.IsMatch(line))
                inCodeBlock = !inCodeBlock;

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

    private static List<(string? Heading, string Content)> MergeSmallSections(
        List<(string? Heading, string Content)> sections,
        int minChunkSize)
    {
        if (sections.Count <= 1) return sections;

        var merged = new List<(string? Heading, string Content)>();
        foreach (var section in sections)
        {
            if (merged.Count > 0 && section.Content.Trim().Length < minChunkSize)
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

    private static List<string> SplitWithOverlap(string text, int maxChars, int overlapChars, int minSplitDistance)
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
            end = FindSafeSplitPoint(text, pos, end, minSplitDistance);
            chunks.Add(text[pos..end]);

            if (end >= text.Length) break;
            pos = Math.Max(pos + 1, end - overlapChars);
        }

        return chunks;
    }

    private static int FindSafeSplitPoint(string text, int start, int proposedEnd, int minSplitDistance)
    {
        if (proposedEnd >= text.Length) return text.Length;

        var subText = text[start..proposedEnd];
        var fenceCount = CodeBlockRegex.Matches(subText).Count;

        if (fenceCount % 2 == 1)
        {
            var nextFence = CodeBlockRegex.Match(text, proposedEnd);
            if (nextFence.Success)
            {
                var lineEnd = text.IndexOf('\n', nextFence.Index);
                return lineEnd >= 0 ? lineEnd : text.Length;
            }
            return text.Length;
        }

        var searchEnd = Math.Min(proposedEnd, text.Length - 1);
        var lineBreak = text.LastIndexOf('\n', searchEnd);
        if (lineBreak > start + minSplitDistance) return lineBreak + 1;

        return proposedEnd;
    }

    private static KnowledgeChunk CreateChunk(
        string id,
        string source,
        string category,
        string title,
        string content,
        Dictionary<string, string> metadata)
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

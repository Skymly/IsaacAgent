namespace IsaacAgent.Rag.Chunking;

/// <summary>
/// Tuning knobs for <see cref="MarkdownKnowledgeChunker"/>. Prefer the static presets
/// rather than assembling flags ad hoc — IndexBuilder and tests use those presets.
/// </summary>
public sealed class MarkdownChunkOptions
{
    /// <summary>Maximum chunk size in characters (≈512 tokens for embedding).</summary>
    public int MaxChunkSize { get; init; } = 2000;

    /// <summary>Overlap between adjacent chunks to preserve context.</summary>
    public int OverlapChars { get; init; } = 200;

    /// <summary>
    /// Minimum section size: merge-small threshold (patterns/examples) and MkDocs
    /// short-circuit for tiny documents.
    /// </summary>
    public int MinChunkSize { get; init; } = 100;

    /// <summary>
    /// Minimum distance from the chunk start before snapping a split to a line break.
    /// </summary>
    public int MinSplitDistance { get; init; } = 100;

    /// <summary>Strip MkDocs Material attribute annotations and flatten admonitions.</summary>
    public bool CleanMkDocsSyntax { get; init; }

    /// <summary>Resolve category from YAML tags and path heuristics (MkDocs docs).</summary>
    public bool UseMkDocsCategoryResolution { get; init; }

    /// <summary>Prefer H1 title over front-matter / file name (MkDocs docs).</summary>
    public bool PreferH1DocTitle { get; init; }

    /// <summary>Merge sections smaller than <see cref="MinChunkSize"/> into the previous section.</summary>
    public bool MergeSmallSections { get; init; }

    /// <summary>Add <c>file</c> metadata with the relative markdown path.</summary>
    public bool IncludeFileMetadata { get; init; }

    /// <summary>
    /// IsaacDocs / REPENTOGON embedded markdown: cleanup, tags→category, H1 titles.
    /// </summary>
    public static MarkdownChunkOptions ForMkDocsDocs { get; } = new()
    {
        CleanMkDocsSyntax = true,
        UseMkDocsCategoryResolution = true,
        PreferH1DocTitle = true,
    };

    /// <summary>
    /// Pattern and example markdown: merge tiny sections, attach file metadata,
    /// category/title from front matter.
    /// </summary>
    public static MarkdownChunkOptions ForPatternsOrExamples { get; } = new()
    {
        MergeSmallSections = true,
        IncludeFileMetadata = true,
    };
}

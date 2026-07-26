namespace IsaacAgent.Agent.Engine;

/// <summary>
/// Captured prior file state (or create tombstone) recorded lazily before a
/// Tracked write first mutates a path after a Checkpoint.
/// </summary>
public sealed class BeforeImage
{
    public BeforeImage(string relativePath, bool isTombstone, string? content)
    {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        IsTombstone = isTombstone;
        Content = content;
        if (isTombstone && content is not null)
            throw new ArgumentException("Tombstone Before-images must not carry content.", nameof(content));
        if (!isTombstone && content is null)
            throw new ArgumentException("Content Before-images require content.", nameof(content));
    }

    /// <summary>
    /// Project-relative path key (forward slashes), matching Tracked write targets.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// True when the path did not exist before the Tracked write (create tombstone).
    /// </summary>
    public bool IsTombstone { get; }

    /// <summary>
    /// UTF-8 text content when the file existed; null for tombstones.
    /// </summary>
    public string? Content { get; }
}

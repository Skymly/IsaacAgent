using IsaacAgent.Core.Models;

namespace IsaacAgent.Agent.Engine;

/// <summary>
/// In-session conversation anchor created before a user message is processed.
/// </summary>
public sealed class Checkpoint
{
    private readonly Dictionary<string, BeforeImage> _beforeImages =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _touchedPaths =
        new(StringComparer.OrdinalIgnoreCase);

    public Checkpoint(Guid id, ChatMessage userMessage)
    {
        Id = id;
        UserMessage = userMessage ?? throw new ArgumentNullException(nameof(userMessage));
    }

    public Guid Id { get; }

    /// <summary>
    /// The user <see cref="ChatMessage"/> this Checkpoint anchors. Identity
    /// (reference) is the conversation cursor — valid while the message
    /// remains in the session history.
    /// </summary>
    public ChatMessage UserMessage { get; }

    /// <summary>
    /// Lazily captured Before-images for paths first touched by a Tracked write
    /// after this Checkpoint. Skipped paths (binary / over-limit / unsafe) are
    /// not present; they are marked touched so a later write does not invent a
    /// late capture.
    /// </summary>
    public IReadOnlyDictionary<string, BeforeImage> BeforeImages => _beforeImages;

    /// <summary>
    /// Paths touched after this Checkpoint that have no usable Before-image
    /// (binary / over-limit / unsafe skips). Restore lists these as
    /// <c>missing-before-image</c>.
    /// </summary>
    internal IEnumerable<string> TouchedPathsWithoutBeforeImage =>
        _touchedPaths.Where(p => !_beforeImages.ContainsKey(p));

    internal bool HasTouchedPath(string relativePath) =>
        _touchedPaths.Contains(relativePath) || _beforeImages.ContainsKey(relativePath);

    /// <summary>
    /// Records a successful Before-image on first touch. Returns false if the
    /// path was already touched (capture or skip).
    /// </summary>
    internal bool TryRecordBeforeImage(BeforeImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!_touchedPaths.Add(image.RelativePath))
            return false;

        _beforeImages[image.RelativePath] = image;
        return true;
    }

    /// <summary>
    /// Marks a path as touched without storing a usable Before-image (skip).
    /// </summary>
    internal bool TryMarkPathTouched(string relativePath) =>
        _touchedPaths.Add(relativePath);
}

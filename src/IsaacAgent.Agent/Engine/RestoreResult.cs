namespace IsaacAgent.Agent.Engine;

/// <summary>
/// Outcome of restoring a live session to a Checkpoint.
/// </summary>
public sealed class RestoreResult
{
    public RestoreResult(
        Guid checkpointId,
        string userPrompt,
        IReadOnlyList<string> restoredPaths,
        IReadOnlyList<RestoreSkippedPath> skippedPaths)
    {
        CheckpointId = checkpointId;
        UserPrompt = userPrompt ?? throw new ArgumentNullException(nameof(userPrompt));
        RestoredPaths = restoredPaths ?? throw new ArgumentNullException(nameof(restoredPaths));
        SkippedPaths = skippedPaths ?? throw new ArgumentNullException(nameof(skippedPaths));
    }

    public Guid CheckpointId { get; }

    /// <summary>
    /// The Checkpoint user-turn prompt (for App input refill).
    /// </summary>
    public string UserPrompt { get; }

    public IReadOnlyList<string> RestoredPaths { get; }

    public IReadOnlyList<RestoreSkippedPath> SkippedPaths { get; }
}

/// <summary>
/// A path left unchanged during Restore, with a machine-readable reason.
/// </summary>
public sealed class RestoreSkippedPath
{
    public RestoreSkippedPath(string relativePath, string reason)
    {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    public string RelativePath { get; }

    /// <summary>
    /// Skip reason: <c>missing-before-image</c>, <c>hand-edit</c>, or <c>unreadable</c>.
    /// </summary>
    public string Reason { get; }
}

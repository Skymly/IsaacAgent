namespace IsaacAgent.App.Services;

/// <summary>
/// Confirm dialog for Checkpoint Restore. Distinct from loading a saved chat
/// session from the Chat session store (project reopen / hydrate).
/// </summary>
public interface IRestoreConfirmDialog
{
    /// <summary>
    /// Shows the Restore confirm dialog with the five required facts.
    /// Returns <c>true</c> when the user confirms; <c>false</c> when dismissed.
    /// </summary>
    Task<bool> ConfirmRestoreAsync(RestoreConfirmCopy copy, CancellationToken ct = default);
}

/// <summary>
/// Localized (or fallback) copy for the Restore confirm dialog.
/// </summary>
public sealed record RestoreConfirmCopy(
    string Title,
    string TruncateFact,
    string BeforeImageFact,
    string CancelInFlightFact,
    string RefillInputFact,
    string UntrackedFact);

using Avalonia;

namespace IsaacAgent.App.Services;

/// <summary>
/// Builds Restore confirm copy from localized resources with English fallbacks.
/// </summary>
public static class RestoreConfirmCopyFactory
{
    public static RestoreConfirmCopy Create() => new(
        Title: Resolve("ChatRestoreTitle", "Restore (Checkpoint)"),
        TruncateFact: Resolve(
            "ChatRestoreFactTruncate",
            "The conversation will be truncated from this user turn onward."),
        BeforeImageFact: Resolve(
            "ChatRestoreFactBeforeImage",
            "Tracked writes will be reverted via Before-images under the current Hand-edit conflict mode."),
        CancelInFlightFact: Resolve(
            "ChatRestoreFactCancelInFlight",
            "If a generation is in progress, it will be cancelled."),
        RefillInputFact: Resolve(
            "ChatRestoreFactRefillInput",
            "This user prompt will be put back into the input box."),
        UntrackedFact: Resolve(
            "ChatRestoreFactUntracked",
            "run_command and other untracked side effects will not be reverted."));

    private static string Resolve(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is string s
            && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        return fallback;
    }
}

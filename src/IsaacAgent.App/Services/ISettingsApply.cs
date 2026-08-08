namespace IsaacAgent.App.Services;

/// <summary>
/// Settings apply: make the running session match provider intent
/// (chat provider swap; optionally kick off Embedding apply).
/// Also owns explicit knowledge-index rebuild so it shares in-flight
/// cancellation with Embedding apply.
/// </summary>
public interface ISettingsApply
{
    /// <summary>
    /// Applies chat provider immediately. When embedding-related fields changed,
    /// starts Embedding apply in the background and returns without waiting.
    /// A newer apply that needs rebuild cancels any in-flight rebuild (including
    /// an explicit RebuildIndexAsync) and waits for it to finish before clearing
    /// the store.
    /// </summary>
    void Apply(ProviderIntent intent, ISettingsApplyProgress progress);

    /// <summary>
    /// Rebuilds the knowledge index explicitly. Shares the same in-flight gate as
    /// Embedding apply: cancels any prior rebuild, waits for it to exit, then rebuilds.
    /// </summary>
    Task RebuildIndexAsync(ISettingsApplyProgress progress);
}

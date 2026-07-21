namespace IsaacAgent.App.Services;

/// <summary>
/// Settings apply: make the running session match provider intent
/// (chat provider swap; optionally kick off Embedding apply).
/// </summary>
public interface ISettingsApply
{
    /// <summary>
    /// Applies chat provider immediately. When embedding-related fields changed,
    /// starts Embedding apply in the background and returns without waiting.
    /// A newer apply that needs rebuild cancels any in-flight rebuild.
    /// </summary>
    void Apply(ProviderIntent intent, ISettingsApplyProgress progress);
}

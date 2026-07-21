namespace IsaacAgent.App.Services;

/// <summary>
/// Progress/results from Settings apply. Callers (Settings) update UI/toasts;
/// apply must not service-locate the ViewModel.
/// </summary>
public interface ISettingsApplyProgress
{
    void OnRebuildStarted();
    void OnRebuildSucceeded(string status);
    void OnRebuildFailed(string status);
    void OnRebuildFinished();
}

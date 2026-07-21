using IsaacAgent.App.ViewModels;

namespace IsaacAgent.App.Services;

/// <summary>
/// Bridges Settings apply progress to Settings UI status/toasts without
/// Settings apply resolving the ViewModel via a service locator.
/// </summary>
public sealed class SettingsApplyProgress : ISettingsApplyProgress
{
    private readonly SettingsViewModel _settings;
    private readonly ToastService? _toasts;

    public SettingsApplyProgress(SettingsViewModel settings, ToastService? toasts = null)
    {
        _settings = settings;
        _toasts = toasts;
    }

    public void OnRebuildStarted()
    {
        _settings.SetIndexRebuilding(true);
        _settings.SetIndexStatus("Building knowledge index...");
    }

    public void OnRebuildSucceeded(string status)
    {
        _settings.SetIndexStatus(status);
        _toasts?.ShowSuccess(status);
    }

    public void OnRebuildFailed(string status)
    {
        _settings.SetIndexStatus(status);
        _toasts?.ShowError(status);
    }

    public void OnRebuildFinished()
    {
        _settings.SetIndexRebuilding(false);
    }
}

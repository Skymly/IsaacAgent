using System.Collections.ObjectModel;
using Avalonia.Threading;
using IsaacAgent.App.ViewModels;

namespace IsaacAgent.App.Services;

/// <summary>
///   Manages a queue of transient toast notifications. The UI binds to
///   <see cref="ActiveToasts"/> and renders them as an overlay. Toasts
///   auto-dismiss after their duration expires.
/// </summary>
public sealed class ToastService
{
    private const int DefaultDurationMs = 4000;

    /// <summary>
    ///   When true, toasts auto-dismiss after their duration.
    ///   Set to false in tests to prevent background timers from affecting
    ///   unrelated assertions.
    /// </summary>
    internal bool AutoDismissEnabled { get; set; } = true;

    /// <summary>
    ///   Tests only — when set, replaces the default auto-dismiss scheduling.
    /// </summary>
    internal Action<ToastNotification, int>? TestDismissScheduler { get; set; }

    public ObservableCollection<ToastNotification> ActiveToasts { get; } = [];

    public void ShowInfo(string message, int durationMs = DefaultDurationMs)
        => Show(message, ToastSeverity.Info, durationMs);

    public void ShowSuccess(string message, int durationMs = DefaultDurationMs)
        => Show(message, ToastSeverity.Success, durationMs);

    public void ShowWarning(string message, int durationMs = DefaultDurationMs)
        => Show(message, ToastSeverity.Warning, durationMs);

    public void ShowError(string message, int durationMs = 6000)
        => Show(message, ToastSeverity.Error, durationMs);

    private void Show(string message, ToastSeverity severity, int durationMs)
    {
        var toast = new ToastNotification(message, severity, durationMs);
        ActiveToasts.Add(toast);

        if (!AutoDismissEnabled)
            return;

        if (TestDismissScheduler is { } scheduler)
        {
            scheduler(toast, durationMs);
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(durationMs).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() => Dismiss(toast));
        });
    }

    public void Dismiss(ToastNotification toast)
    {
        ActiveToasts.Remove(toast);
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace IsaacAgent.App.ViewModels;

/// <summary>
///   Severity level for toast notifications.
/// </summary>
public enum ToastSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
///   A single toast notification shown briefly in the UI overlay.
/// </summary>
public sealed partial class ToastNotification : ObservableObject
{
    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private ToastSeverity _severity = ToastSeverity.Info;

    /// <summary>
    ///   Remaining display time in milliseconds. The UI decrements
    ///   this and removes the toast when it reaches zero.
    /// </summary>
    [ObservableProperty]
    private int _remainingMs;

    public ToastNotification(string message, ToastSeverity severity = ToastSeverity.Info, int durationMs = 4000)
    {
        _message = message;
        _severity = severity;
        _remainingMs = durationMs;
    }

    public string Icon => Severity switch
    {
        ToastSeverity.Success => "✓",
        ToastSeverity.Warning => "⚠",
        ToastSeverity.Error => "✗",
        _ => "ℹ"
    };
}

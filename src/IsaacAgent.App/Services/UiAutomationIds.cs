using Avalonia.Automation;
using Avalonia.Controls;

namespace IsaacAgent.App.Services;

/// <summary>
/// Stable AutomationId contract for FlaUI / Nightly UI automation (issue #79).
/// Exact strings are documented in docs/design/App.md — do not rename lightly.
/// </summary>
internal static class UiAutomationIds
{
    public const string MainWindow = "MainWindow";
    public const string StatusSurface = "StatusSurface";
    public const string FileTree = "FileTree";

    /// <summary>
    /// Attaches the FlaUI AutomationId contract to the main shell controls.
    /// </summary>
    public static void Attach(Window mainWindow, Control statusSurface, Control fileTree)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(statusSurface);
        ArgumentNullException.ThrowIfNull(fileTree);

        AutomationProperties.SetAutomationId(mainWindow, MainWindow);
        AutomationProperties.SetAutomationId(statusSurface, StatusSurface);
        AutomationProperties.SetAutomationId(fileTree, FileTree);
    }
}

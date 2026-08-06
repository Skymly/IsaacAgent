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
    public const string MenuFile = "MenuFile";
    public const string MenuFileSettings = "MenuFileSettings";
    public const string SettingsWindow = "SettingsWindow";
    public const string ChatInput = "ChatInput";

    /// <summary>
    /// Attaches the FlaUI AutomationId contract to the main shell controls.
    /// </summary>
    public static void AttachMainShell(
        Window mainWindow,
        Control statusSurface,
        Control fileTree,
        MenuItem fileMenu,
        MenuItem settingsMenuItem,
        TextBox chatInput)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(statusSurface);
        ArgumentNullException.ThrowIfNull(fileTree);
        ArgumentNullException.ThrowIfNull(fileMenu);
        ArgumentNullException.ThrowIfNull(settingsMenuItem);
        ArgumentNullException.ThrowIfNull(chatInput);

        AutomationProperties.SetAutomationId(mainWindow, MainWindow);
        AutomationProperties.SetAutomationId(statusSurface, StatusSurface);
        AutomationProperties.SetAutomationId(fileTree, FileTree);
        AutomationProperties.SetAutomationId(fileMenu, MenuFile);
        AutomationProperties.SetAutomationId(settingsMenuItem, MenuFileSettings);
        AutomationProperties.SetAutomationId(chatInput, ChatInput);
    }

    /// <summary>
    /// Attaches the FlaUI AutomationId contract to the Settings window root.
    /// </summary>
    public static void AttachSettingsWindow(Window settingsWindow)
    {
        ArgumentNullException.ThrowIfNull(settingsWindow);

        AutomationProperties.SetAutomationId(settingsWindow, SettingsWindow);
    }
}

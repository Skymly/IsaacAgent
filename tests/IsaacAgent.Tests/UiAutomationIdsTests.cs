using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using IsaacAgent.App.Services;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// Stable AutomationId contract for FlaUI (issue #79).
/// </summary>
[Collection("Avalonia")]
public class UiAutomationIdsTests
{
    [Fact]
    public void Contract_Ids_AreStableLiterals()
    {
        Assert.Equal("MainWindow", UiAutomationIds.MainWindow);
        Assert.Equal("StatusSurface", UiAutomationIds.StatusSurface);
        Assert.Equal("FileTree", UiAutomationIds.FileTree);
        Assert.Equal("MenuFile", UiAutomationIds.MenuFile);
        Assert.Equal("MenuFileSettings", UiAutomationIds.MenuFileSettings);
        Assert.Equal("SettingsWindow", UiAutomationIds.SettingsWindow);
        Assert.Equal("ChatInput", UiAutomationIds.ChatInput);
    }

    [AvaloniaFact]
    public void AttachMainShell_SetsAutomationIdsOnControls()
    {
        var window = new Window();
        var status = new Border();
        var fileTree = new TreeView();
        var fileMenu = new MenuItem();
        var settingsMenuItem = new MenuItem();
        var chatInput = new TextBox();

        UiAutomationIds.AttachMainShell(
            window,
            status,
            fileTree,
            fileMenu,
            settingsMenuItem,
            chatInput);

        Assert.Equal(UiAutomationIds.MainWindow, AutomationProperties.GetAutomationId(window));
        Assert.Equal(UiAutomationIds.StatusSurface, AutomationProperties.GetAutomationId(status));
        Assert.Equal(UiAutomationIds.FileTree, AutomationProperties.GetAutomationId(fileTree));
        Assert.Equal(UiAutomationIds.MenuFile, AutomationProperties.GetAutomationId(fileMenu));
        Assert.Equal(
            UiAutomationIds.MenuFileSettings,
            AutomationProperties.GetAutomationId(settingsMenuItem));
        Assert.Equal(UiAutomationIds.ChatInput, AutomationProperties.GetAutomationId(chatInput));
    }

    [AvaloniaFact]
    public void AttachSettingsWindow_SetsAutomationIdOnWindowRoot()
    {
        var window = new Window();

        UiAutomationIds.AttachSettingsWindow(window);

        Assert.Equal(
            UiAutomationIds.SettingsWindow,
            AutomationProperties.GetAutomationId(window));
    }
}

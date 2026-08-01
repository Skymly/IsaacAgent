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
    }

    [AvaloniaFact]
    public void Attach_SetsAutomationIdsOnControls()
    {
        var window = new Window();
        var status = new Border();
        var fileTree = new TreeView();

        UiAutomationIds.Attach(window, status, fileTree);

        Assert.Equal(UiAutomationIds.MainWindow, AutomationProperties.GetAutomationId(window));
        Assert.Equal(UiAutomationIds.StatusSurface, AutomationProperties.GetAutomationId(status));
        Assert.Equal(UiAutomationIds.FileTree, AutomationProperties.GetAutomationId(fileTree));
    }
}

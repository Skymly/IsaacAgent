using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Xunit;

namespace IsaacAgent.UiTests;

/// <summary>
/// UI-only Settings reachability via File menu — no Apply (issue #88).
/// </summary>
[Collection("Ui")]
public sealed class SettingsSmokeTests
{
    [Fact]
    public void Launch_OpenSettingsViaFileMenu_ThenCloseSettings_ThenCleanExit()
    {
        using var session = AppSession.Launch();

        session.WaitForAutomationId(UiContract.MainWindow, TimeSpan.FromSeconds(60));
        session.BringMainWindowToForeground();

        var settingsItem = OpenSettingsMenuItem(session);
        InvokeMenuItem(settingsItem);

        session.WaitForAutomationId(UiContract.SettingsWindow, TimeSpan.FromSeconds(30));
        session.CloseWindowByAutomationId(UiContract.SettingsWindow, TimeSpan.FromSeconds(30));

        Assert.Null(session.TryFindAutomationId(UiContract.SettingsWindow, TimeSpan.FromSeconds(5)));

        _ = session.CloseMainWindowCleanly(TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Avalonia MenuItem does not expose ExpandCollapse; Click opens File.
    /// Avoid re-Click while open (toggles closed); Escape before retry.
    /// </summary>
    private static AutomationElement OpenSettingsMenuItem(AppSession session)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
        Exception? last = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                session.BringMainWindowToForeground();

                var alreadyOpen = session.TryFindAutomationId(
                    UiContract.MenuFileSettings,
                    TimeSpan.FromMilliseconds(400));
                if (alreadyOpen is not null)
                    return alreadyOpen;

                var fileMenu = session.WaitForAutomationId(UiContract.MenuFile, TimeSpan.FromSeconds(5));
                ExpandFileMenu(fileMenu);

                var settings = session.TryFindAutomationId(UiContract.MenuFileSettings, TimeSpan.FromSeconds(3));
                if (settings is not null)
                    return settings;

                // Menu may have toggled closed or never opened — reset before next Click.
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                Thread.Sleep(150);
            }
            catch (Exception ex)
            {
                last = ex;
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                Thread.Sleep(150);
            }
        }

        throw new TimeoutException(
            "MenuFileSettings not found after Expand/Click on MenuFile within 00:00:45.",
            last);
    }

    private static void ExpandFileMenu(AutomationElement fileMenu)
    {
        var menuItem = fileMenu.AsMenuItem();
        if (menuItem.Patterns.ExpandCollapse.IsSupported)
            menuItem.Expand();
        else
            menuItem.Click(moveMouse: true);

        Thread.Sleep(300);
    }

    private static void InvokeMenuItem(AutomationElement item)
    {
        var menuItem = item.AsMenuItem();
        if (menuItem.Patterns.Invoke.IsSupported)
            menuItem.Invoke();
        else
            menuItem.Click(moveMouse: true);
    }
}

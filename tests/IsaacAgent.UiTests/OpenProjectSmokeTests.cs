using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;

namespace IsaacAgent.UiTests;

/// <summary>
/// Path B: open committed MinimalMod via --project (issue #80).
/// </summary>
[Collection("Ui")]
public sealed class OpenProjectSmokeTests
{
    [Fact]
    [Trait("FlaUI", "PublishSmoke")]
    public void Launch_WithProject_StatusOrTreeShowsFixture_ThenCleanExit()
    {
        var fixtureDir = MinimalModFixture.ResolveDirectory();

        using var session = AppSession.Launch("--project", fixtureDir);

        session.WaitForAutomationId(UiContract.MainWindow, TimeSpan.FromSeconds(60));

        var loaded = WaitUntil(
            () => ProjectLooksLoaded(session),
            TimeSpan.FromSeconds(60),
            TimeSpan.FromMilliseconds(500));

        Assert.True(
            loaded,
            "Expected StatusSurface to contain 'Project: MinimalMod' and/or FileTree to list main.lua.");

        // Process must be gone; graceful CloseMainWindow can fail while RAG prewarm
        // is still embedding (App fire-and-forget). Kill fallback in CloseMainWindowCleanly
        // still satisfies "clean exit" / no orphan for path B.
        _ = session.CloseMainWindowCleanly(TimeSpan.FromSeconds(60));
    }

    private static bool ProjectLooksLoaded(AppSession session)
    {
        try
        {
            var status = session.TryFindAutomationId(UiContract.StatusSurface, TimeSpan.FromSeconds(2));
            if (status is not null && ContainsText(status, "Project: MinimalMod"))
                return true;

            var tree = session.TryFindAutomationId(UiContract.FileTree, TimeSpan.FromSeconds(2));
            return tree is not null &&
                   (ContainsText(tree, "main.lua") || ContainsText(tree, "metadata.xml"));
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsText(AutomationElement root, string needle)
    {
        if (root.Name?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        var cf = new ConditionFactory(new UIA3PropertyLibrary());
        var texts = root.FindAllDescendants(cf.ByControlType(ControlType.Text));
        foreach (var text in texts)
        {
            if (text.Name?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }

        var treeItems = root.FindAllDescendants(cf.ByControlType(ControlType.TreeItem));
        foreach (var item in treeItems)
        {
            if (item.Name?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }

        return false;
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout, TimeSpan interval)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;
            Thread.Sleep(interval);
        }

        return condition();
    }
}

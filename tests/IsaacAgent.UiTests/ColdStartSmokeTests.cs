using Xunit;

namespace IsaacAgent.UiTests;

/// <summary>
/// Path A: cold-start smoke through the real App window (issue #80).
/// </summary>
[Collection("Ui")]
public sealed class ColdStartSmokeTests
{
    [Fact]
    public void Launch_ReleaseApp_MainWindowAppears_ThenCleanExit()
    {
        using var session = AppSession.Launch();

        session.WaitForAutomationId(UiContract.MainWindow, TimeSpan.FromSeconds(60));

        var closedGracefully = session.CloseMainWindowCleanly(TimeSpan.FromSeconds(30));
        Assert.True(closedGracefully, "Expected cold-start App to exit without force-kill.");
    }
}

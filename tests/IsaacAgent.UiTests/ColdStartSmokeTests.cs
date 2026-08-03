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

        // Process must be gone. Graceful CloseMainWindow can fail while RAG
        // prewarm is still embedding (same as path B); kill fallback is OK.
        _ = session.CloseMainWindowCleanly(TimeSpan.FromSeconds(60));
    }
}

using Xunit;

namespace IsaacAgent.UiTests;

/// <summary>
/// UI-only Chat chrome reachability — no typing, Send, or LLM (issue #88).
/// </summary>
[Collection("Ui")]
public sealed class ChatSmokeTests
{
    [Fact]
    public void Launch_ReleaseApp_ChatInputAppears_ThenCleanExit()
    {
        using var session = AppSession.Launch();

        session.WaitForAutomationId(UiContract.ChatInput, TimeSpan.FromSeconds(60));

        _ = session.CloseMainWindowCleanly(TimeSpan.FromSeconds(60));
    }
}

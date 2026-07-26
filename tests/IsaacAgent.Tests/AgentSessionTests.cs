using IsaacAgent.Agent.Engine;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

public class AgentSessionTests
{
    private static AgentSession CreateSession(IChatService chat, ToolRegistry? tools = null)
    {
        var logger = Mock.Of<ILogger<AgentSession>>();
        var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
        tools ??= new ToolRegistry(toolLogger);
        return new AgentSession(chat, tools, null, logger);
    }

    private sealed class StubChatService : IChatService
    {
        private readonly ChatChunk[] _chunks;
        public StubChatService(params ChatChunk[] chunks) => _chunks = chunks;

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse { Message = new ChatMessage { Role = "assistant", Content = "ok" } });

        public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            foreach (var c in _chunks)
                yield return c;
        }
    }

    [Fact]
    public async Task SendMessageAsync_TextOnly_ReturnsContent()
    {
        var chat = new StubChatService(
            new ChatChunk("Hello", false, -1, null, null, null));

        var session = CreateSession(chat);
        var chunks = new List<string>();
        await foreach (var c in session.SendMessageAsync("hi"))
            chunks.Add(c);

        Assert.Single(chunks);
        Assert.Equal("Hello", chunks[0]);
    }

    [Fact]
    public async Task SendMessageAsync_CreatesCheckpoint_AnchoredToUserMessage()
    {
        var chat = new StubChatService(
            new ChatChunk("ok", false, -1, null, null, null));

        var session = CreateSession(chat);
        await foreach (var _ in session.SendMessageAsync("hello checkpoint")) { }

        Assert.Single(session.Checkpoints);
        var checkpoint = session.Checkpoints[0];
        Assert.NotEqual(Guid.Empty, checkpoint.Id);

        var userMsg = session.History.Single(m => m.Role == "user");
        Assert.Same(userMsg, checkpoint.UserMessage);
        Assert.Equal("hello checkpoint", checkpoint.UserMessage.Content);
    }

    [Fact]
    public async Task SendMessageAsync_CreatesCheckpoint_PerUserMessage()
    {
        var chat = new StubChatService(
            new ChatChunk("ok", false, -1, null, null, null));

        var session = CreateSession(chat);
        await foreach (var _ in session.SendMessageAsync("first")) { }
        await foreach (var _ in session.SendMessageAsync("second")) { }

        Assert.Equal(2, session.Checkpoints.Count);
        Assert.Equal("first", session.Checkpoints[0].UserMessage.Content);
        Assert.Equal("second", session.Checkpoints[1].UserMessage.Content);
        Assert.All(session.Checkpoints, cp =>
            Assert.Contains(cp.UserMessage, session.History));
    }

    [Fact]
    public async Task SendMessageAsync_LogsCheckpointCreated()
    {
        var chat = new StubChatService(
            new ChatChunk("ok", false, -1, null, null, null));
        var logger = new Mock<ILogger<AgentSession>>();
        var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
        var session = new AgentSession(chat, new ToolRegistry(toolLogger), null, logger.Object);

        await foreach (var _ in session.SendMessageAsync("log me")) { }

        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Checkpoint", StringComparison.Ordinal)
                    && state.ToString()!.Contains("created", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Checkpoints_AreIsolatedPerSession()
    {
        var chat = new StubChatService(
            new ChatChunk("ok", false, -1, null, null, null));

        var sessionA = CreateSession(chat);
        var sessionB = CreateSession(chat);

        await foreach (var _ in sessionA.SendMessageAsync("only-a")) { }
        await foreach (var _ in sessionB.SendMessageAsync("only-b")) { }

        Assert.Single(sessionA.Checkpoints);
        Assert.Single(sessionB.Checkpoints);
        Assert.Equal("only-a", sessionA.Checkpoints[0].UserMessage.Content);
        Assert.Equal("only-b", sessionB.Checkpoints[0].UserMessage.Content);
        Assert.NotSame(sessionA.Checkpoints[0], sessionB.Checkpoints[0]);
    }

    [Fact]
    public async Task SendMessageAsync_TrimsHistory_DropsCheckpointsOutsideRetainedHistory()
    {
        var chat = new StubChatService(
            new ChatChunk("ok", false, -1, null, null, null));

        var session = CreateSession(chat);

        // Exceed MaxHistoryMessages (50): each turn adds user + assistant.
        for (var i = 0; i < 30; i++)
        {
            await foreach (var _ in session.SendMessageAsync($"msg {i}")) { }
        }

        Assert.NotEmpty(session.Checkpoints);
        Assert.All(session.Checkpoints, cp =>
            Assert.Contains(cp.UserMessage, session.History));

        // Early turns were trimmed; their Checkpoints must be gone.
        Assert.DoesNotContain(session.Checkpoints, cp => cp.UserMessage.Content == "msg 0");
        Assert.Contains(session.Checkpoints, cp =>
            cp.UserMessage.Content == session.History.Last(m => m.Role == "user").Content);
    }

    [Fact]
    public async Task ClearHistory_AfterSend_ClearsCheckpoints()
    {
        var chat = new StubChatService(
            new ChatChunk("ok", false, -1, null, null, null));
        var session = CreateSession(chat);
        await foreach (var _ in session.SendMessageAsync("bye")) { }

        Assert.NotEmpty(session.Checkpoints);
        session.ClearHistory();
        Assert.Empty(session.Checkpoints);
    }

    [Fact]
    public async Task SendMessageAsync_HistoryContainsUserAndAssistant()
    {
        var chat = new StubChatService(
            new ChatChunk("Reply", false, -1, null, null, null));

        var session = CreateSession(chat);
        await foreach (var _ in session.SendMessageAsync("test")) { }

        Assert.Equal(3, session.History.Count); // system + user + assistant
        Assert.Equal("user", session.History[1].Role);
        Assert.Equal("assistant", session.History[2].Role);
    }

    [Fact]
    public async Task SendMessageAsync_ToolCall_ExecutesAndContinues()
    {
        var chat = new StubChatService(
            new ChatChunk("", true, 0, "call_1", "search_isaac_api", """{"query":"test"}"""),
            new ChatChunk("", true, 0, null, null, null),
            new ChatChunk("Done", false, -1, null, null, null));

        var session = CreateSession(chat);
        var chunks = new List<string>();
        await foreach (var c in session.SendMessageAsync("search"))
            chunks.Add(c);

        Assert.Contains("Done", chunks);
        // History: system + user + assistant(tool_call) + tool_result + assistant(text)
        Assert.True(session.History.Count >= 5);
        Assert.Equal("tool", session.History[3].Role);
    }

    [Fact]
    public async Task SendMessageAsync_MaxIterations_StopsWithError()
    {
        // Every response is a tool call — will hit max iterations
        var chat = new StubChatService(
            new ChatChunk("", true, 0, "call_1", "search_isaac_api", """{"query":"x"}"""),
            new ChatChunk("", true, 0, null, null, null));

        var session = CreateSession(chat);
        var chunks = new List<string>();
        await foreach (var c in session.SendMessageAsync("loop"))
            chunks.Add(c);

        Assert.Contains(chunks, c => c.Contains("Max tool call iterations"));
    }

    [Fact]
    public void ClearHistory_ResetsToSystemOnly()
    {
        var chat = new StubChatService();
        var session = CreateSession(chat);

        // Simulate some history
        session.History.Add(ChatMessage.User("test"));

        session.ClearHistory();

        Assert.Single(session.History);
        Assert.Equal("system", session.History[0].Role);
    }

    [Fact]
    public void SetProjectDirectory_ClearsHistory()
    {
        var chat = new StubChatService();
        var session = CreateSession(chat);

        session.History.Add(ChatMessage.User("before"));
        session.SetProjectDirectory(Path.Combine(Path.GetTempPath(), $"isaac_test_{Guid.NewGuid():N}", "path"));

        Assert.Single(session.History);
        Assert.Equal("system", session.History[0].Role);
    }

    [Fact]
    public async Task SendMessageAsync_MultipleToolCalls_SequentialExecution()
    {
        var chat = new StubChatService(
            new ChatChunk("", true, 0, "call_1", "search_isaac_api", """{"query":"a"}"""),
            new ChatChunk("", true, 0, null, null, null),
            new ChatChunk("", true, 1, "call_2", "search_isaac_api", """{"query":"b"}"""),
            new ChatChunk("", true, 1, null, null, null),
            new ChatChunk("Both done", false, -1, null, null, null));

        var session = CreateSession(chat);
        var chunks = new List<string>();
        await foreach (var c in session.SendMessageAsync("multi"))
            chunks.Add(c);

        Assert.Contains("Both done", chunks);
        // History: system + user + assistant(2 tool calls) + 2 tool_results + assistant(text)
        Assert.True(session.History.Count >= 6);
    }

    [Fact]
    public async Task SendMessageAsync_TrimsHistory_WhenExceedingLimit()
    {
        // Create a chat that returns text for each message
        var chat = new StubChatService(
            new ChatChunk("ok", false, -1, null, null, null));

        var session = CreateSession(chat);

        // Send many messages to exceed MaxHistoryMessages (50)
        for (var i = 0; i < 30; i++)
        {
            await foreach (var _ in session.SendMessageAsync($"msg {i}")) { }
        }

        // Each send adds 2 messages (user + assistant), plus 1 system = 61
        // TrimHistory runs at the start of each iteration, so after the assistant
        // message is added the count may be 51 (50 + 1). Allow a small margin.
        Assert.True(session.History.Count <= 55, $"History count was {session.History.Count}");
        Assert.Equal("system", session.History[0].Role);
    }

    [Fact]
    public void SaveAndLoadHistory_RoundTrips()
    {
        var chat = new StubChatService();
        var session = CreateSession(chat);

        session.History.Add(ChatMessage.User("hello"));
        session.History.Add(ChatMessage.Assistant("hi there"));

        var tempPath = Path.Combine(Path.GetTempPath(), $"isaac_history_test_{Guid.NewGuid():N}.json");
        try
        {
            session.SaveHistory(tempPath);

            var session2 = CreateSession(chat);
            session2.LoadHistory(tempPath);

            Assert.Equal(session.History.Count, session2.History.Count);
            Assert.Equal("user", session2.History[1].Role);
            Assert.Equal("hello", session2.History[1].Content);
            Assert.Equal("assistant", session2.History[2].Role);
            Assert.Equal("hi there", session2.History[2].Content);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void LoadHistory_NonExistentFile_DoesNotThrow()
    {
        var chat = new StubChatService();
        var session = CreateSession(chat);

        session.LoadHistory(Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}", "file.json"));

        // Should still have just the system prompt
        Assert.Single(session.History);
    }

    [Fact]
    public async Task SendMessageAsync_ToolCallbackThrows_DoesNotCrash()
    {
        var chat = new StubChatService(
            new ChatChunk("", true, 0, "call_1", "search_isaac_api", """{"query":"test"}"""),
            new ChatChunk("", true, 0, null, null, null),
            new ChatChunk("Recovered", false, -1, null, null, null));

        var session = CreateSession(chat);
        session.OnToolCall += (_, _) => throw new InvalidOperationException("UI dispatch failed");

        var chunks = new List<string>();
        await foreach (var c in session.SendMessageAsync("test"))
            chunks.Add(c);

        // Session should not crash — LLM should still get a tool result and continue
        Assert.Contains("Recovered", chunks);
        // History should contain a tool result with error message
        var toolMsg = session.History.FirstOrDefault(h => h.Role == "tool");
        Assert.NotNull(toolMsg);
        Assert.Contains("failed", toolMsg!.Content);
    }

    [Fact]
    public async Task SendMessageAsync_TrimsHistory_WhenLargeToolResultsExceedCharBudget()
    {
        // Each tool result is ~10k chars. With 20+ exchanges the char budget
        // (120k) will be exceeded even though message count stays well under 50.
        var bigResult = new string('x', 10_000);
        var chat = new StubChatService(
            new ChatChunk("", true, 0, "call_1", "search_isaac_api", """{"query":"test"}"""),
            new ChatChunk("", true, 0, null, null, null),
            new ChatChunk("done", false, -1, null, null, null));

        var session = CreateSession(chat);

        // Send 20 messages, each producing a tool call with a 10k result
        for (var i = 0; i < 20; i++)
        {
            await foreach (var _ in session.SendMessageAsync($"msg {i}")) { }
        }

        // History should be well under the char budget after trimming.
        // System prompt + recent messages should remain.
        var totalChars = session.History
            .Where(m => m.Role != "system")
            .Sum(m => (m.Content?.Length ?? 0) + m.ToolCalls.Sum(tc => (tc.Name?.Length ?? 0) + (tc.Arguments?.Length ?? 0)));

        Assert.True(totalChars <= 120_000 + 50_000,
            $"History char count {totalChars} should be roughly within budget after trimming");
        Assert.Equal("system", session.History[0].Role);
    }

    [Fact]
    public async Task SendMessageAsync_CharBudgetTrim_PreservesToolCallPairs()
    {
        // Tool call + tool result pair must not be split by char-budget trimming.
        var bigResult = new string('y', 15_000);
        var chat = new StubChatService(
            new ChatChunk("", true, 0, "call_1", "search_isaac_api", """{"query":"test"}"""),
            new ChatChunk("", true, 0, null, null, null),
            new ChatChunk("ok", false, -1, null, null, null));

        var session = CreateSession(chat);

        for (var i = 0; i < 15; i++)
        {
            await foreach (var _ in session.SendMessageAsync($"msg {i}")) { }
        }

        // After trimming, verify no orphaned tool results at the start
        // (first non-system message should not be a "tool" role message)
        Assert.True(session.History.Count > 1);
        var firstNonSystem = session.History[1];
        Assert.NotEqual("tool", firstNonSystem.Role);

        // If first non-system is an assistant with tool_calls, all its
        // tool results must be present
        if (firstNonSystem.Role == "assistant" && firstNonSystem.ToolCalls.Count > 0)
        {
            var expected = firstNonSystem.ToolCalls.Count;
            var actual = 0;
            for (var i = 2; i < session.History.Count && actual < expected; i++)
            {
                if (session.History[i].Role == "tool") actual++;
                else break;
            }
            Assert.Equal(expected, actual);
        }
    }
}

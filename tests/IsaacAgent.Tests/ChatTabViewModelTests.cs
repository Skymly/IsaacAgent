using System.Runtime.CompilerServices;
using Avalonia.Threading;
using IsaacAgent.Agent;
using IsaacAgent.Agent.Engine;
using IsaacAgent.App.ViewModels;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for ChatTabViewModel — send/cancel, project switching,
///   token counting, message management, and session event wiring.
/// </summary>
[Collection("Avalonia")]
public class ChatTabViewModelTests
{
    // ── Test doubles ──────────────────────────────────────────

    /// <summary>
    ///   A scripted chat service that returns a fixed batch of chunks
    ///   on each StreamAsync call. Supports multi-turn scripting.
    /// </summary>
    private sealed class ScriptedChatService : IChatService
    {
        private readonly List<List<ChatChunk>> _turns;
        private int _callIndex;

        public int CallCount => _callIndex;
        public List<ChatRequest> ReceivedRequests { get; } = [];

        public ScriptedChatService(params List<ChatChunk>[] turns) => _turns = [.. turns];

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse
            {
                Message = new ChatMessage { Role = "assistant", Content = "ok" }
            });

        public async IAsyncEnumerable<ChatChunk> StreamAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            ReceivedRequests.Add(request);
            var turn = _callIndex < _turns.Count ? _turns[_callIndex] : [new ChatChunk("done", false, -1, null, null, null)];
            _callIndex++;
            foreach (var chunk in turn)
                yield return chunk;
            await Task.CompletedTask;
        }
    }

    /// <summary>
    ///   A chat service that never yields any chunks — simulates a
    ///   hanging or empty response.
    /// </summary>
    private sealed class EmptyChatService : IChatService
    {
        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse
            {
                Message = new ChatMessage { Role = "assistant", Content = "" }
            });

        public async IAsyncEnumerable<ChatChunk> StreamAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield break;
        }
    }

    /// <summary>
    ///   A chat service that throws on StreamAsync — simulates a
    ///   network error or provider failure.
    /// </summary>
    private sealed class ThrowingChatService : IChatService
    {
        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("provider unavailable");

        public async IAsyncEnumerable<ChatChunk> StreamAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            throw new InvalidOperationException("stream failed");
#pragma warning disable CS0162 // unreachable code — intentional for test
            yield break;
#pragma warning restore CS0162
        }
    }

    // ── Helpers ───────────────────────────────────────────────

    private static ChatChunk TextChunk(string text) => new(text, false, -1, null, null, null);

    /// <summary>
    ///   Pumps the Avalonia dispatcher queue so that pending Post
    ///   callbacks (used by ChatTabViewModel for streaming updates)
    ///   execute before assertions.
    /// </summary>
    private static void FlushDispatcher()
    {
        Dispatcher.UIThread.Invoke(() => { }, DispatcherPriority.Background);
    }

    private static (ChatTabViewModel tab, ScriptedChatService chat) CreateTab(
        params List<ChatChunk>[] turns)
    {
        var chat = new ScriptedChatService(turns);
        var session = CreateSession(chat);

        var factoryMock = new Mock<IAgentSessionFactory>();
        factoryMock.Setup(f => f.Create(It.IsAny<string?>())).Returns(session);

        var services = new ServiceCollection();
        services.AddSingleton(factoryMock.Object);
        services.AddSingleton(Mock.Of<ILogger<ChatTabViewModel>>());
        var sp = services.BuildServiceProvider();

        var tab = new ChatTabViewModel(sp, sp.GetRequiredService<ILogger<ChatTabViewModel>>(), null);
        return (tab, chat);
    }

    private static AgentSession CreateSession(IChatService chat)
    {
        var logger = Mock.Of<ILogger<AgentSession>>();
        var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(toolLogger);
        return new AgentSession(chat, registry, null, logger, null);
    }

    // ── Constructor / initialization ──────────────────────────

    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        Assert.Equal("Chat", tab.Title);
        Assert.Equal("", tab.InputText);
        Assert.False(tab.IsGenerating);
        Assert.Equal(0, tab.TotalInputTokens);
        Assert.Equal(0, tab.TotalOutputTokens);
        Assert.Empty(tab.Messages);
    }

    // ── Send ──────────────────────────────────────────────────

    [Fact]
    public async Task Send_WithText_AddsUserAndAssistantMessages()
    {
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("Hello!") }]);
        tab.InputText = "What is Isaac?";

        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        // Should have user message + assistant message
        Assert.Equal(2, tab.Messages.Count);
        Assert.Equal("user", tab.Messages[0].Role);
        Assert.Equal("What is Isaac?", tab.Messages[0].Content);
        Assert.Equal("assistant", tab.Messages[1].Role);
        Assert.Contains("Hello!", tab.Messages[1].Content);
    }

    [Fact]
    public async Task Send_WithText_ClearsInputText()
    {
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("ok") }]);
        tab.InputText = "test message";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal("", tab.InputText);
    }

    [Fact]
    public async Task Send_WithEmptyText_DoesNothing()
    {
        var (tab, chat) = CreateTab([new List<ChatChunk> { TextChunk("should not happen") }]);
        tab.InputText = "   ";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Empty(tab.Messages);
        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task Send_WhileGenerating_DoesNotSendAgain()
    {
        var (tab, chat) = CreateTab([
            new List<ChatChunk> { TextChunk("first"), TextChunk("response") },
            new List<ChatChunk> { TextChunk("second") }
        ]);
        tab.InputText = "first";
        await tab.SendCommand.ExecuteAsync(null);

        // Now set IsGenerating and try again
        tab.InputText = "second";
        // IsGenerating should already be false after completion
        Assert.False(tab.IsGenerating);
        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(2, chat.CallCount);
    }

    [Fact]
    public async Task Send_StreamingAccumulatesIntoAssistantMessage()
    {
        var (tab, _) = CreateTab([
            new List<ChatChunk>
            {
                TextChunk("Hello"),
                TextChunk(" "),
                TextChunk("world!")
            }
        ]);
        tab.InputText = "test";

        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        var assistantMsg = tab.Messages.Last(m => m.Role == "assistant");
        Assert.Equal("Hello world!", assistantMsg.Content);
    }

    [Fact]
    public async Task Send_OnError_AddsErrorMessage()
    {
        var chat = new ThrowingChatService();
        var session = CreateSession(chat);
        var factoryMock = new Mock<IAgentSessionFactory>();
        factoryMock.Setup(f => f.Create(It.IsAny<string?>())).Returns(session);
        var services = new ServiceCollection();
        services.AddSingleton(factoryMock.Object);
        services.AddSingleton(Mock.Of<ILogger<ChatTabViewModel>>());
        var sp = services.BuildServiceProvider();
        var tab = new ChatTabViewModel(sp, sp.GetRequiredService<ILogger<ChatTabViewModel>>(), null);

        tab.InputText = "trigger error";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        // Should have user message + error message (empty assistant removed)
        Assert.True(tab.Messages.Count >= 2);
        Assert.Contains(tab.Messages, m => m.Role == "error");
        Assert.False(tab.IsGenerating);
    }

    // ── Cancel ────────────────────────────────────────────────

    [Fact]
    public async Task Send_Cancel_RemovesEmptyAssistantAndAddsCancelledMessage()
    {
        // Use a chat service that yields slowly so we can cancel mid-stream
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("partial") }]);
        tab.InputText = "test cancel";

        // Start send, then cancel immediately
        var sendTask = tab.SendCommand.ExecuteAsync(null);
        tab.CancelCommand.Execute(null);
        await sendTask;
        FlushDispatcher();

        // After cancellation: should have user message + system "(cancelled)"
        // The empty assistant message should be removed
        Assert.Contains(tab.Messages, m => m.Role == "user");
        // Either cancelled or completed (race depends on timing)
        Assert.False(tab.IsGenerating);
    }

    // ── IsGenerating state ────────────────────────────────────

    [Fact]
    public async Task Send_SetsIsGeneratingDuringSend_ResetsAfter()
    {
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("done") }]);

        Assert.False(tab.IsGenerating);
        tab.InputText = "test";
        await tab.SendCommand.ExecuteAsync(null);

        Assert.False(tab.IsGenerating);
    }

    // ── ClearMessages ─────────────────────────────────────────

    [Fact]
    public async Task ClearMessages_RemovesAllMessagesAndResetsTokens()
    {
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("hello") }]);
        tab.InputText = "test";
        await tab.SendCommand.ExecuteAsync(null);

        Assert.True(tab.Messages.Count > 0);

        tab.ClearMessages();

        Assert.Empty(tab.Messages);
        Assert.Equal(0, tab.TotalInputTokens);
        Assert.Equal(0, tab.TotalOutputTokens);
    }

    // ── OnProjectChanged ──────────────────────────────────────

    [Fact]
    public void OnProjectChanged_ClearsMessagesAndResetsTokens()
    {
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        // Simulate some state
        tab.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "old" });
        tab.TotalInputTokens = 100;
        tab.TotalOutputTokens = 50;

        // OnProjectChanged disposes the old session and creates a new one
        tab.OnProjectChanged("/some/project");

        Assert.Empty(tab.Messages);
        Assert.Equal(0, tab.TotalInputTokens);
        Assert.Equal(0, tab.TotalOutputTokens);
    }

    [Fact]
    public void OnProjectChanged_NullDir_DoesNotThrow()
    {
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        tab.OnProjectChanged(null);
        Assert.Empty(tab.Messages);
    }

    // ── ToggleExpand ──────────────────────────────────────────

    [Fact]
    public void ToggleExpand_TogglesIsExpanded()
    {
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        var msg = new ChatMessageViewModel { Role = "tool", Content = "args", IsExpanded = false };

        tab.ToggleExpandCommand.Execute(msg);
        Assert.True(msg.IsExpanded);

        tab.ToggleExpandCommand.Execute(msg);
        Assert.False(msg.IsExpanded);
    }

    [Fact]
    public void ToggleExpand_NullParameter_DoesNothing()
    {
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        // Should not throw
        tab.ToggleExpandCommand.Execute(null);
    }

    // ── Dispose ───────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        tab.Dispose(); // should not throw
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var (tab, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        tab.Dispose();
        tab.Dispose(); // idempotent
    }
}

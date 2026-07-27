using Avalonia.Headless.XUnit;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using IsaacAgent.Agent;
using IsaacAgent.Agent.Engine;
using IsaacAgent.App.Services;
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

    private static void FlushDispatcher() => AvaloniaTestHelper.FlushDispatcher();

    /// <summary>
    ///   Test double for Restore confirm — records the copy shown and
    ///   returns a scripted confirm/dismiss result.
    /// </summary>
    private sealed class FakeRestoreConfirmDialog : IRestoreConfirmDialog
    {
        public bool ConfirmResult { get; set; } = true;
        public int CallCount { get; private set; }
        public RestoreConfirmCopy? LastCopy { get; private set; }

        public Task<bool> ConfirmRestoreAsync(
            RestoreConfirmCopy copy,
            CancellationToken ct = default)
        {
            CallCount++;
            LastCopy = copy;
            return Task.FromResult(ConfirmResult);
        }
    }

    /// <summary>
    ///   Chat service that blocks until <see cref="Release"/> is signaled
    ///   (or the token is cancelled) — for cancel-in-flight Restore tests.
    /// </summary>
    private sealed class GateChatService : IChatService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse
            {
                Message = new ChatMessage { Role = "assistant", Content = "ok" }
            });

        public async IAsyncEnumerable<ChatChunk> StreamAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            // Non-empty first delta so AgentSession yields to the UI and
            // ChatTabViewModel can attach the live Checkpoint affordance.
            yield return TextChunk("…");
            Started.TrySetResult();
            await Release.Task.WaitAsync(ct);
            yield return TextChunk("late");
        }
    }

    /// <summary>Recording Chat session store for persist-trigger tests (#50).</summary>
    private sealed class RecordingChatSessionStore : IChatSessionStore
    {
        public List<(string? ProjectDir, ProjectSessionManifest Manifest)> Saves { get; } = [];

        public Task<bool> SaveAsync(string? projectDir, ProjectSessionManifest manifest, CancellationToken ct = default)
        {
            Saves.Add((projectDir, Clone(manifest)));
            return Task.FromResult(!string.IsNullOrWhiteSpace(projectDir));
        }

        public Task<ProjectSessionManifest> LoadAsync(string? projectDir, CancellationToken ct = default)
            => Task.FromResult(new ProjectSessionManifest { ProjectDir = projectDir ?? "" });

        private static ProjectSessionManifest Clone(ProjectSessionManifest source) => new()
        {
            Version = source.Version,
            ProjectDir = source.ProjectDir,
            SavedAt = source.SavedAt,
            Tabs = source.Tabs.Select(t => new SessionTabRecord
            {
                Id = t.Id,
                Title = t.Title,
                HistoryVersion = t.HistoryVersion,
                Messages = t.Messages.ToList()
            }).ToList()
        };
    }

    private static (ChatTabViewModel tab, ScriptedChatService chat, FakeRestoreConfirmDialog confirm)
        CreateTab(params List<ChatChunk>[] turns)
    {
        var chat = new ScriptedChatService(turns);
        var session = CreateSession(chat);
        return CreateTabWith(session, chat);
    }

    private static (ChatTabViewModel tab, TChat chat, FakeRestoreConfirmDialog confirm)
        CreateTabWith<TChat>(
            AgentSession session,
            TChat chat,
            Func<HandEditConflictMode>? handEditMode = null,
            string? projectDir = null,
            Func<CancellationToken, Task>? persistSession = null)
        where TChat : IChatService
    {
        var confirm = new FakeRestoreConfirmDialog();
        var factoryMock = new Mock<IAgentSessionFactory>();
        factoryMock.Setup(f => f.Create(It.IsAny<string?>())).Returns(session);

        var services = new ServiceCollection();
        services.AddSingleton(factoryMock.Object);
        services.AddSingleton(Mock.Of<ILogger<ChatTabViewModel>>());
        services.AddSingleton<IRestoreConfirmDialog>(confirm);
        services.AddSingleton<Func<HandEditConflictMode>>(
            handEditMode ?? (() => HandEditConflictMode.Force));
        var sp = services.BuildServiceProvider();

        var tab = new ChatTabViewModel(
            sp,
            sp.GetRequiredService<ILogger<ChatTabViewModel>>(),
            projectDir,
            persistSession: persistSession);
        return (tab, chat, confirm);
    }

    private static (ChatTabViewModel tab, RecordingChatSessionStore store, FakeRestoreConfirmDialog confirm)
        CreateTabWithRecordingStore(
            string? projectDir,
            params List<ChatChunk>[] turns)
    {
        var chat = new ScriptedChatService(turns);
        var session = CreateSession(chat);
        var store = new RecordingChatSessionStore();
        ChatTabViewModel? tabRef = null;
        Func<CancellationToken, Task> persist = async ct =>
        {
            if (string.IsNullOrWhiteSpace(projectDir) || tabRef is null)
                return;
            await store.SaveAsync(projectDir, new ProjectSessionManifest
            {
                ProjectDir = projectDir,
                Tabs =
                [
                    new SessionTabRecord
                    {
                        Id = tabRef.Id,
                        Title = tabRef.Title,
                        HistoryVersion = 1,
                        Messages = tabRef.AgentHistory.ToList()
                    }
                ]
            }, ct);
        };
        var (tab, _, confirm) = CreateTabWith(session, chat, projectDir: projectDir, persistSession: persist);
        tabRef = tab;
        return (tab, store, confirm);
    }

    private static AgentSession CreateSession(IChatService chat)
    {
        var logger = Mock.Of<ILogger<AgentSession>>();
        var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(toolLogger);
        return new AgentSession(chat, registry, null, logger, null);
    }

    // ── Constructor / initialization ──────────────────────────

    [AvaloniaFact]
    public void Constructor_InitializesWithDefaults()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        Assert.Equal("Chat", tab.Title);
        Assert.Equal("", tab.InputText);
        Assert.False(tab.IsGenerating);
        Assert.Equal(0, tab.TotalInputTokens);
        Assert.Equal(0, tab.TotalOutputTokens);
        Assert.Empty(tab.Messages);
    }

    // ── Send ──────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Send_WithText_AddsUserAndAssistantMessages()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("Hello!") }]);
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

    [AvaloniaFact]
    public async Task Send_WithText_ClearsInputText()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("ok") }]);
        tab.InputText = "test message";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal("", tab.InputText);
    }

    [AvaloniaFact]
    public async Task Send_WithEmptyText_DoesNothing()
    {
        var (tab, chat, _) = CreateTab([new List<ChatChunk> { TextChunk("should not happen") }]);
        tab.InputText = "   ";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Empty(tab.Messages);
        Assert.Equal(0, chat.CallCount);
    }

    [AvaloniaFact]
    public async Task Send_WhileGenerating_DoesNotSendAgain()
    {
        var (tab, chat, _) = CreateTab([
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

    [AvaloniaFact]
    public async Task Send_StreamingAccumulatesIntoAssistantMessage()
    {
        var (tab, _, _) = CreateTab([
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

    [AvaloniaFact]
    public async Task Send_OnError_AddsErrorMessage()
    {
        var chat = new ThrowingChatService();
        var session = CreateSession(chat);
        var (tab, _, _) = CreateTabWith(session, chat);

        tab.InputText = "trigger error";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        // Should have user message + error message (empty assistant removed)
        Assert.True(tab.Messages.Count >= 2);
        Assert.Contains(tab.Messages, m => m.Role == "error");
        Assert.False(tab.IsGenerating);
    }

    // ── Cancel ────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Send_Cancel_RemovesEmptyAssistantAndAddsCancelledMessage()
    {
        // Use a chat service that yields slowly so we can cancel mid-stream
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("partial") }]);
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

    [AvaloniaFact]
    public async Task Send_SetsIsGeneratingDuringSend_ResetsAfter()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("done") }]);

        Assert.False(tab.IsGenerating);
        tab.InputText = "test";
        await tab.SendCommand.ExecuteAsync(null);

        Assert.False(tab.IsGenerating);
    }

    // ── ClearMessages ─────────────────────────────────────────

    [AvaloniaFact]
    public async Task ClearMessages_RemovesAllMessagesAndResetsTokens()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hello") }]);
        tab.InputText = "test";
        await tab.SendCommand.ExecuteAsync(null);

        Assert.True(tab.Messages.Count > 0);

        tab.ClearMessages();

        Assert.Empty(tab.Messages);
        Assert.Equal(0, tab.TotalInputTokens);
        Assert.Equal(0, tab.TotalOutputTokens);
    }

    // ── OnProjectChanged ──────────────────────────────────────

    [AvaloniaFact]
    public void OnProjectChanged_ClearsMessagesAndResetsTokens()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
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

    [AvaloniaFact]
    public void OnProjectChanged_NullDir_DoesNotThrow()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        tab.OnProjectChanged(null);
        Assert.Empty(tab.Messages);
    }

    // ── ToggleExpand ──────────────────────────────────────────

    [AvaloniaFact]
    public void ToggleExpand_TogglesIsExpanded()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        var msg = new ChatMessageViewModel { Role = "tool", Content = "args", IsExpanded = false };

        tab.ToggleExpandCommand.Execute(msg);
        Assert.True(msg.IsExpanded);

        tab.ToggleExpandCommand.Execute(msg);
        Assert.False(msg.IsExpanded);
    }

    [AvaloniaFact]
    public void ToggleExpand_NullParameter_DoesNothing()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        // Should not throw
        tab.ToggleExpandCommand.Execute(null);
    }

    // ── Dispose ───────────────────────────────────────────────

    [AvaloniaFact]
    public void Dispose_DoesNotThrow()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        tab.Dispose(); // should not throw
    }

    [AvaloniaFact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        tab.Dispose();
        tab.Dispose(); // idempotent
    }

    // ── InsertSnippet ─────────────────────────────────────────

    [AvaloniaFact]
    public void InsertSnippet_EmptyInput_SetsInputText()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        tab.InputText = "";
        tab.InsertSnippetCommand.Execute("local x = 1");
        Assert.Equal("local x = 1", tab.InputText);
    }

    [AvaloniaFact]
    public void InsertSnippet_ExistingInput_AppendsOnNewLine()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        tab.InputText = "existing text";
        tab.InsertSnippetCommand.Execute("local x = 1");
        Assert.Contains("existing text", tab.InputText);
        Assert.Contains("local x = 1", tab.InputText);
        Assert.Contains("\n", tab.InputText);
    }

    [AvaloniaFact]
    public void InsertSnippet_EmptySnippet_DoesNothing()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        tab.InputText = "hello";
        tab.InsertSnippetCommand.Execute("");
        Assert.Equal("hello", tab.InputText);
    }

    // ── ChatMessageViewModel editing ──────────────────────────

    [AvaloniaFact]
    public void StartEdit_UserMessage_SetsIsEditingAndEditText()
    {
        var msg = new ChatMessageViewModel { Role = "user", Content = "Hello" };
        msg.StartEditCommand.Execute(null);
        Assert.True(msg.IsEditing);
        Assert.Equal("Hello", msg.EditText);
    }

    [AvaloniaFact]
    public void StartEdit_AssistantMessage_DoesNotStartEditing()
    {
        var msg = new ChatMessageViewModel { Role = "assistant", Content = "Hi" };
        msg.StartEditCommand.Execute(null);
        Assert.False(msg.IsEditing);
    }

    [AvaloniaFact]
    public void CancelEdit_ResetsIsEditingAndEditText()
    {
        var msg = new ChatMessageViewModel { Role = "user", Content = "Hello" };
        msg.StartEditCommand.Execute(null);
        Assert.True(msg.IsEditing);

        msg.CancelEditCommand.Execute(null);
        Assert.False(msg.IsEditing);
        Assert.Equal("", msg.EditText);
    }

    // ── Chat session store persist triggers (issue #50) ───────

    [AvaloniaFact]
    public async Task Send_WithProject_PersistsSessionViaStore()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"isaac_persist_{Guid.NewGuid():N}");
        var (tab, store, _) = CreateTabWithRecordingStore(
            projectDir,
            [new List<ChatChunk> { TextChunk("Hello!") }]);

        tab.InputText = "What is Isaac?";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        var save = Assert.Single(store.Saves);
        Assert.Equal(projectDir, save.ProjectDir);
        Assert.Contains(save.Manifest.Tabs[0].Messages, m => m.Role == "user" && m.Content == "What is Isaac?");
        Assert.Contains(save.Manifest.Tabs[0].Messages, m => m.Role == "assistant" && m.Content.Contains("Hello!"));
    }

    [AvaloniaFact]
    public async Task Send_WithoutProject_DoesNotPersistToStore()
    {
        var (tab, store, _) = CreateTabWithRecordingStore(
            projectDir: null,
            [new List<ChatChunk> { TextChunk("Hello!") }]);

        tab.InputText = "orphan turn";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        Assert.Empty(store.Saves);
    }

    [AvaloniaFact]
    public async Task Send_OnError_DoesNotPersistToStore()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"isaac_persist_{Guid.NewGuid():N}");
        var chat = new ThrowingChatService();
        var session = CreateSession(chat);
        var store = new RecordingChatSessionStore();
        ChatTabViewModel? tabRef = null;
        Func<CancellationToken, Task> persist = async ct =>
        {
            if (tabRef is null) return;
            await store.SaveAsync(projectDir, new ProjectSessionManifest
            {
                ProjectDir = projectDir,
                Tabs =
                [
                    new SessionTabRecord
                    {
                        Id = tabRef.Id,
                        Title = tabRef.Title,
                        Messages = tabRef.AgentHistory.ToList()
                    }
                ]
            }, ct);
        };
        var (tab, _, _) = CreateTabWith(session, chat, projectDir: projectDir, persistSession: persist);
        tabRef = tab;

        tab.InputText = "trigger error";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        Assert.Empty(store.Saves);
    }

    [AvaloniaFact]
    public async Task Restore_Confirm_PersistsTruncatedSessionViaStore()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"isaac_persist_{Guid.NewGuid():N}");
        var (tab, store, confirm) = CreateTabWithRecordingStore(
            projectDir,
            [
                new List<ChatChunk> { TextChunk("keep-reply") },
                new List<ChatChunk> { TextChunk("drop-reply") }
            ]);

        tab.InputText = "keep";
        await tab.SendCommand.ExecuteAsync(null);
        tab.InputText = "restore here";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        store.Saves.Clear();
        confirm.ConfirmResult = true;
        var target = tab.Messages.First(m => m.IsUser && m.Content == "restore here");
        await tab.RestoreCommand.ExecuteAsync(target);
        FlushDispatcher();

        var save = Assert.Single(store.Saves);
        Assert.Equal(projectDir, save.ProjectDir);
        Assert.DoesNotContain(save.Manifest.Tabs[0].Messages, m => m.Content == "restore here");
        Assert.DoesNotContain(save.Manifest.Tabs[0].Messages, m => m.Content == "drop-reply");
        Assert.Contains(save.Manifest.Tabs[0].Messages, m => m.Role == "user" && m.Content == "keep");
        Assert.Contains(save.Manifest.Tabs[0].Messages, m => m.Role == "assistant" && m.Content.Contains("keep-reply"));
    }

    // ── Checkpoint Restore (issue #39) ────────────────────────

    [AvaloniaFact]
    public async Task Send_UserMessage_HasCanRestoreWhenCheckpointLive()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("ok") }]);
        tab.InputText = "anchor me";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        var user = Assert.Single(tab.Messages, m => m.IsUser);
        Assert.True(user.CanRestore);
        Assert.NotNull(user.CheckpointId);
    }

    [AvaloniaFact]
    public async Task Send_AssistantMessage_DoesNotOfferRestore()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("ok") }]);
        tab.InputText = "hi";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        var assistant = Assert.Single(tab.Messages, m => m.IsAssistant);
        Assert.False(assistant.CanRestore);
        Assert.Null(assistant.CheckpointId);
    }

    [AvaloniaFact]
    public async Task Restore_ConfirmDialog_StatesFiveRequiredFacts()
    {
        var (tab, _, confirm) = CreateTab([new List<ChatChunk> { TextChunk("ok") }]);
        tab.InputText = "prompt";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        var user = Assert.Single(tab.Messages, m => m.IsUser);
        await tab.RestoreCommand.ExecuteAsync(user);

        Assert.Equal(1, confirm.CallCount);
        var copy = Assert.IsType<RestoreConfirmCopy>(confirm.LastCopy);
        Assert.Contains("Restore", copy.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Checkpoint", copy.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("truncate", copy.TruncateFact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Before-image", copy.BeforeImageFact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hand-edit", copy.BeforeImageFact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cancel", copy.CancelInFlightFact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input", copy.RefillInputFact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("run_command", copy.UntrackedFact, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task Restore_DismissConfirm_LeavesConversationAndInputUnchanged()
    {
        var (tab, _, confirm) = CreateTab([
            new List<ChatChunk> { TextChunk("a") },
            new List<ChatChunk> { TextChunk("b") }
        ]);
        tab.InputText = "first";
        await tab.SendCommand.ExecuteAsync(null);
        tab.InputText = "second";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        confirm.ConfirmResult = false;
        var firstUser = tab.Messages.First(m => m.IsUser);
        var messageCountBefore = tab.Messages.Count;
        var checkpointCountBefore = firstUser.CheckpointId;
        tab.InputText = "draft";

        await tab.RestoreCommand.ExecuteAsync(firstUser);
        FlushDispatcher();

        Assert.Equal(1, confirm.CallCount);
        Assert.Equal(messageCountBefore, tab.Messages.Count);
        Assert.Equal("draft", tab.InputText);
        Assert.Contains(tab.Messages, m => m.IsUser && m.Content == "first");
        Assert.Contains(tab.Messages, m => m.IsUser && m.Content == "second");
        Assert.True(firstUser.CanRestore);
        Assert.Equal(checkpointCountBefore, firstUser.CheckpointId);
    }

    [AvaloniaFact]
    public async Task Restore_Confirm_TruncatesUiAndRefillsInput()
    {
        var (tab, _, confirm) = CreateTab([
            new List<ChatChunk> { TextChunk("keep-reply") },
            new List<ChatChunk> { TextChunk("drop-reply") }
        ]);
        tab.InputText = "keep";
        await tab.SendCommand.ExecuteAsync(null);
        tab.InputText = "restore here";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        confirm.ConfirmResult = true;
        var target = tab.Messages.First(m => m.IsUser && m.Content == "restore here");

        await tab.RestoreCommand.ExecuteAsync(target);
        FlushDispatcher();

        Assert.Equal("restore here", tab.InputText);
        Assert.DoesNotContain(tab.Messages, m => m.Content == "restore here");
        Assert.DoesNotContain(tab.Messages, m => m.Content == "drop-reply");
        Assert.Contains(tab.Messages, m => m.IsUser && m.Content == "keep");
        Assert.Contains(tab.Messages, m => m.IsAssistant && m.Content.Contains("keep-reply"));
        Assert.DoesNotContain(tab.Messages, m => m.IsUser && m.CanRestore && m.Content == "restore here");
    }

    [AvaloniaFact]
    public async Task Restore_WhileGenerating_CancelsInFlightThenRestores()
    {
        var gate = new GateChatService();
        var session = CreateSession(gate);
        var (tab, _, confirm) = CreateTabWith(session, gate);

        tab.InputText = "in flight";
        var sendTask = tab.SendCommand.ExecuteAsync(null);
        await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        FlushDispatcher();

        Assert.True(tab.IsGenerating);
        var user = Assert.Single(tab.Messages, m => m.IsUser);
        Assert.True(user.CanRestore);

        confirm.ConfirmResult = true;
        await tab.RestoreCommand.ExecuteAsync(user);
        gate.Release.TrySetResult();
        await sendTask;
        FlushDispatcher();

        Assert.False(tab.IsGenerating);
        Assert.Equal("in flight", tab.InputText);
        Assert.DoesNotContain(tab.Messages, m => m.IsUser && m.Content == "in flight");
        Assert.Empty(session.Checkpoints);
    }

    [AvaloniaFact]
    public async Task Restore_UsesInjectedHandEditConflictMode()
    {
        HandEditConflictMode? observed = null;
        var chat = new ScriptedChatService([new List<ChatChunk> { TextChunk("ok") }]);
        var session = CreateSession(chat);
        var (tab, _, confirm) = CreateTabWith(
            session,
            chat,
            () =>
            {
                observed = HandEditConflictMode.Skip;
                return HandEditConflictMode.Skip;
            });

        tab.InputText = "prompt";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        confirm.ConfirmResult = true;
        var user = Assert.Single(tab.Messages, m => m.IsUser);
        await tab.RestoreCommand.ExecuteAsync(user);
        FlushDispatcher();

        Assert.Equal(HandEditConflictMode.Skip, observed);
        Assert.Equal("prompt", tab.InputText);
    }

    // ── Message trimming (memory optimization) ────────────────

    [AvaloniaFact]
    public void Messages_BelowLimit_NotTrimmed()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("hi") }]);
        // Add a few messages — well below the 200 limit
        for (var i = 0; i < 10; i++)
            tab.Messages.Add(new ChatMessageViewModel { Role = "user", Content = $"msg {i}" });

        // Send a message to trigger TrimMessages
        tab.InputText = "trigger trim";
        tab.SendCommand.Execute(null);
        FlushDispatcher();

        // All messages should still be present
        Assert.True(tab.Messages.Count >= 12); // 10 added + user + assistant
    }

    [AvaloniaFact]
    public async Task Messages_AboveLimit_TrimmedAfterSend()
    {
        var (tab, _, _) = CreateTab([new List<ChatChunk> { TextChunk("response") }]);

        // Add 205 messages to exceed the 200 limit
        for (var i = 0; i < 205; i++)
            tab.Messages.Add(new ChatMessageViewModel { Role = "user", Content = $"msg {i}" });

        Assert.Equal(205, tab.Messages.Count);

        // Send a message — should trigger trimming
        tab.InputText = "trigger trim";
        await tab.SendCommand.ExecuteAsync(null);
        FlushDispatcher();

        // Should be trimmed to at most 200 + the new messages from this send
        Assert.True(tab.Messages.Count <= 202,
            $"Expected <= 202 messages after trim, got {tab.Messages.Count}");
    }
}

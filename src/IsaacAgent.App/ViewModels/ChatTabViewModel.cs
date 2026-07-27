using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.Agent;
using IsaacAgent.Agent.Engine;
using IsaacAgent.App.Services;
using IsaacAgent.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.ViewModels;

/// <summary>
/// A single chat tab with its own AgentSession, message history, and token counts.
/// Multiple tabs can coexist, each with independent conversation context.
/// </summary>
public sealed partial class ChatTabViewModel : ObservableObject, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ChatTabViewModel> _logger;
    private readonly IAgentSessionFactory _sessionFactory;
    private readonly IRestoreConfirmDialog _restoreConfirm;
    private readonly Func<HandEditConflictMode> _getHandEditConflictMode;
    private AgentSession _session;
    private CancellationTokenSource? _cts;
    private Task? _sendTask;
    private int _historyEpoch;
    private bool _isRestoring;
    private string? _currentProjectDir;

    /// <summary>Stable tab identity for Chat session store round-trips.</summary>
    public Guid Id { get; }

    /// <summary>Live Agent history envelope (authoritative for persistence / hydrate).</summary>
    public IReadOnlyList<ChatMessage> AgentHistory => _session.History;

    /// <summary>Live session for headless wiring tests (Checkpoints stay ephemeral).</summary>
    internal AgentSession Session => _session;

    private Action<string, string>? _onToolCall;
    private Action<string, string, TimeSpan>? _onToolResult;
    private Action<string>? _onError;
    private Action<int, int>? _onTokenUsage;
    private Action<string, IReadOnlyList<RetrievalResult>>? _onRetrievalResults;

    [ObservableProperty]
    private string _title = "Chat";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _inputText = "";

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private int _totalInputTokens;

    [ObservableProperty]
    private int _totalOutputTokens;

    /// <summary>
    ///   Maximum number of messages kept in the UI collection. Older
    ///   messages are trimmed to prevent unbounded memory growth.
    ///   The full history is still preserved in the AgentSession for
    ///   LLM context.
    /// </summary>
    private const int MaxUiMessages = 200;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public ChatTabViewModel(
        IServiceProvider services,
        ILogger<ChatTabViewModel> logger,
        string? projectDir = null,
        Guid? id = null)
    {
        _services = services;
        _logger = logger;
        Id = id ?? Guid.NewGuid();
        _sessionFactory = services.GetRequiredService<IAgentSessionFactory>();
        _restoreConfirm = services.GetRequiredService<IRestoreConfirmDialog>();
        _getHandEditConflictMode = services.GetService<Func<HandEditConflictMode>>()
            ?? (() =>
            {
                try { return AppConfiguration.Load().HandEditConflictMode; }
                catch { return HandEditConflictMode.Force; }
            });
        _session = _sessionFactory.Create(projectDir);
        _currentProjectDir = projectDir;
        SubscribeSessionEvents(_session);
    }

    private void SubscribeSessionEvents(AgentSession session)
    {
        _onToolCall = (name, args) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                Messages.Add(new ChatMessageViewModel
                {
                    Role = "tool",
                    Content = args,
                    ToolName = name,
                    IsToolCall = true
                }));
        };
        _onToolResult = (result, toolName, elapsed) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                Messages.Add(new ChatMessageViewModel
                {
                    Role = "tool_result",
                    Content = result,
                    ToolName = toolName,
                    ToolDuration = elapsed,
                    IsToolResult = true
                }));
        };
        _onError = (err) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                Messages.Add(new ChatMessageViewModel
                {
                    Role = "error",
                    Content = $"Error: {err}"
                }));
        };
        _onTokenUsage = (input, output) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TotalInputTokens += input;
                TotalOutputTokens += output;
            });
        };
        _onRetrievalResults = (query, results) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (results.Count == 0) return;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"**Knowledge retrieved for:** {query}\n");
                for (var i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    sb.AppendLine($"- **{r.Chunk.Title}** [{r.Chunk.Source}/{r.Chunk.Category}] — score: {r.Score:F3}");
                }
                Messages.Add(new ChatMessageViewModel
                {
                    Role = "retrieval",
                    Content = sb.ToString()
                });
            });
        };

        session.OnToolCall += _onToolCall;
        session.OnToolResult += _onToolResult;
        session.OnError += _onError;
        session.OnTokenUsage += _onTokenUsage;
        session.OnRetrievalResults += _onRetrievalResults;
    }

    private void UnsubscribeSessionEvents(AgentSession session)
    {
        if (_onToolCall is not null) session.OnToolCall -= _onToolCall;
        if (_onToolResult is not null) session.OnToolResult -= _onToolResult;
        if (_onError is not null) session.OnError -= _onError;
        if (_onTokenUsage is not null) session.OnTokenUsage -= _onTokenUsage;
        if (_onRetrievalResults is not null) session.OnRetrievalResults -= _onRetrievalResults;
    }

    public void OnProjectChanged(string? projectDir)
    {
        _currentProjectDir = projectDir;
        UnsubscribeSessionEvents(_session);
        _session.Dispose();
        _session = _sessionFactory.Create(projectDir);
        SubscribeSessionEvents(_session);
        ClearUiMessages();
        // Chat session store is the authoritative hydrate path (#49);
        // do not load legacy per-tab history/ here.
    }

    /// <summary>
    /// Hydrates the live <see cref="AgentSession"/> with the full Agent envelope,
    /// then projects user/assistant rows into the UI bubble list.
    /// When the envelope omits a system prompt (e.g. legacy UI-only migration),
    /// keeps the session's existing project system message.
    /// </summary>
    public void HydrateFromEnvelope(IReadOnlyList<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        ClearUiMessages();

        if (messages.Count > 0)
        {
            ChatMessage? retainedSystem = null;
            if (!messages.Any(m => m.Role == "system")
                && _session.History.Count > 0
                && _session.History[0].Role == "system")
            {
                retainedSystem = _session.History[0];
            }

            _session.History.Clear();
            if (retainedSystem is not null)
                _session.History.Add(retainedSystem);
            _session.History.AddRange(messages);
        }

        ProjectUiBubblesFromHistory();
    }

    private void ProjectUiBubblesFromHistory()
    {
        foreach (var msg in _session.History)
        {
            if (msg.Role is not ("user" or "assistant"))
                continue;
            Messages.Add(new ChatMessageViewModel
            {
                Role = msg.Role,
                Content = msg.Content
            });
        }
    }

    private void ClearUiMessages()
    {
        foreach (var msg in Messages)
            msg.Dispose();
        Messages.Clear();
        TotalInputTokens = 0;
        TotalOutputTokens = 0;
    }

    private string GetHistoryPath(string? projectDir)
    {
        // Legacy per-tab path kept for ScheduleHistorySave until #50 retires it.
        var shortId = Id.ToString("N")[..8];
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IsaacAgent", "history");
        if (string.IsNullOrEmpty(projectDir))
            return Path.Combine(baseDir, $"default_{shortId}.json");

        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(projectDir.ToLowerInvariant()));
        var hash = Convert.ToHexString(hashBytes)[..12];
        return Path.Combine(baseDir, $"project_{hash}_{shortId}.json");
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsGenerating || _isRestoring) return;

        var userMsg = InputText.Trim();
        InputText = "";

        _cts = new CancellationTokenSource();
        IsGenerating = true;

        var userVm = new ChatMessageViewModel { Role = "user", Content = userMsg };
        Messages.Add(userVm);

        var assistantMsg = new ChatMessageViewModel { Role = "assistant", Content = "" };
        Messages.Add(assistantMsg);

        _sendTask = RunSendAsync(userMsg, userVm, assistantMsg, _cts.Token);
        await _sendTask;
    }

    private async Task RunSendAsync(
        string userMsg,
        ChatMessageViewModel userVm,
        ChatMessageViewModel assistantMsg,
        CancellationToken ct)
    {
        try
        {
            await foreach (var chunk in _session.SendMessageAsync(userMsg, ct))
            {
                if (userVm.CheckpointId is null)
                    SyncCheckpointAffordances();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => assistantMsg.Content += chunk);
            }
        }
        catch (OperationCanceledException)
        {
            if (string.IsNullOrEmpty(assistantMsg.Content))
                Messages.Remove(assistantMsg);
            Messages.Add(new ChatMessageViewModel { Role = "system", Content = "(cancelled)" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Send failed");
            if (string.IsNullOrEmpty(assistantMsg.Content))
                Messages.Remove(assistantMsg);
            Messages.Add(new ChatMessageViewModel
            {
                Role = "error",
                Content = $"Error: {ex.Message}"
            });
        }
        finally
        {
            SyncCheckpointAffordances();
            IsGenerating = false;
            _cts?.Dispose();
            _cts = null;
            _sendTask = null;
            ScheduleHistorySave();
            TrimMessages();
        }
    }

    /// <summary>
    /// Restore to the Checkpoint on a user message after confirm: cancel
    /// in-flight generation if needed, invoke session Restore under the
    /// configured Hand-edit conflict mode, truncate UI, and refill the input
    /// with the restored prompt.
    /// </summary>
    [RelayCommand]
    private async Task RestoreAsync(ChatMessageViewModel? msg)
    {
        if (msg is null || !msg.IsUser || _isRestoring) return;

        SyncCheckpointAffordances();
        if (msg.CheckpointId is not Guid checkpointId) return;

        var copy = RestoreConfirmCopyFactory.Create();
        if (!await _restoreConfirm.ConfirmRestoreAsync(copy))
            return;

        _isRestoring = true;
        try
        {
            if (IsGenerating)
            {
                _cts?.Cancel();
                if (_sendTask is not null)
                {
                    try { await _sendTask; }
                    catch (OperationCanceledException) { }
                }
            }

            var result = await _session.RestoreAsync(
                checkpointId,
                _getHandEditConflictMode());

            var idx = Messages.IndexOf(msg);
            if (idx >= 0)
            {
                while (Messages.Count > idx)
                {
                    var last = Messages[^1];
                    last.Dispose();
                    Messages.RemoveAt(Messages.Count - 1);
                }
            }

            InputText = result.UserPrompt;
            SyncCheckpointAffordances();
            // Invalidate any in-flight history save from the cancelled send, then persist.
            _historyEpoch++;
            ScheduleHistorySave();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkpoint Restore failed");
            Messages.Add(new ChatMessageViewModel
            {
                Role = "error",
                Content = $"Error: {ex.Message}"
            });
        }
        finally
        {
            _isRestoring = false;
        }
    }

    private void ScheduleHistorySave()
    {
        var epoch = _historyEpoch;
        var historyPath = GetHistoryPath(_currentProjectDir);
        _ = Task.Run(async () =>
        {
            if (epoch != _historyEpoch)
                return;
            try
            {
                await _session.SaveHistoryAsync(historyPath, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save chat history");
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// Aligns UI user-message <see cref="ChatMessageViewModel.CheckpointId"/> with
    /// live session Checkpoints. Aligns from the end so MaxUiMessages trim
    /// (oldest UI rows dropped first) does not mis-attach affordances.
    /// </summary>
    private void SyncCheckpointAffordances()
    {
        foreach (var m in Messages)
        {
            if (m.IsUser)
                m.CheckpointId = null;
        }

        var histUsers = _session.History.Where(h => h.Role == "user").ToList();
        var uiUsers = Messages.Where(m => m.IsUser).ToList();
        if (uiUsers.Count == 0 || histUsers.Count == 0)
            return;

        // UI trim removes oldest rows; remaining UI users map to the newest
        // history users.
        var histOffset = Math.Max(0, histUsers.Count - uiUsers.Count);

        foreach (var checkpoint in _session.Checkpoints)
        {
            var histIdx = histUsers.IndexOf(checkpoint.UserMessage);
            if (histIdx < 0)
                continue;
            var uiIdx = histIdx - histOffset;
            if (uiIdx < 0 || uiIdx >= uiUsers.Count)
                continue;
            uiUsers[uiIdx].CheckpointId = checkpoint.Id;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    /// <summary>
    ///   Resend an edited user message: removes all messages after the
    ///   edited message, updates its content, and sends again.
    /// </summary>
    [RelayCommand]
    private async Task ResendAsync(ChatMessageViewModel? msg)
    {
        if (msg is null || !msg.IsUser || IsGenerating || _isRestoring) return;
        var newText = msg.EditText.Trim();
        if (string.IsNullOrEmpty(newText)) return;

        // Find the message index and remove all messages after it.
        var idx = Messages.IndexOf(msg);
        if (idx < 0) return;

        msg.IsEditing = false;
        msg.Content = newText;

        // Remove all messages after the edited user message.
        while (Messages.Count > idx + 1)
        {
            var last = Messages[^1];
            last.Dispose();
            Messages.RemoveAt(Messages.Count - 1);
        }

        // Trim session history back to this point — rebuild from remaining messages.
        _session.ClearHistory();
        foreach (var m in Messages)
        {
            if (m.Role is "user" or "assistant")
                _session.History.Add(new ChatMessage { Role = m.Role, Content = m.Content });
        }

        // Now send the edited message.
        _cts = new CancellationTokenSource();
        IsGenerating = true;

        var assistantMsg = new ChatMessageViewModel { Role = "assistant", Content = "" };
        Messages.Add(assistantMsg);

        _sendTask = RunResendAsync(newText, assistantMsg, _cts.Token);
        await _sendTask;
    }

    private async Task RunResendAsync(
        string newText,
        ChatMessageViewModel assistantMsg,
        CancellationToken ct)
    {
        try
        {
            await foreach (var chunk in _session.SendMessageAsync(newText, ct))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => assistantMsg.Content += chunk);
            }
        }
        catch (OperationCanceledException)
        {
            if (string.IsNullOrEmpty(assistantMsg.Content))
                Messages.Remove(assistantMsg);
            Messages.Add(new ChatMessageViewModel { Role = "system", Content = "(cancelled)" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend failed");
            if (string.IsNullOrEmpty(assistantMsg.Content))
                Messages.Remove(assistantMsg);
            Messages.Add(new ChatMessageViewModel
            {
                Role = "error",
                Content = $"Error: {ex.Message}"
            });
        }
        finally
        {
            SyncCheckpointAffordances();
            IsGenerating = false;
            _cts?.Dispose();
            _cts = null;
            _sendTask = null;
            ScheduleHistorySave();
            TrimMessages();
        }
    }

    /// <summary>
    ///   Insert a Lua code snippet into the input box at the cursor position.
    /// </summary>
    [RelayCommand]
    private void InsertSnippet(string snippet)
    {
        if (string.IsNullOrEmpty(snippet)) return;
        if (string.IsNullOrEmpty(InputText))
        {
            InputText = snippet;
        }
        else
        {
            InputText = InputText.TrimEnd() + "\n" + snippet;
        }
    }

    [RelayCommand]
    private void ToggleExpand(ChatMessageViewModel? msg)
    {
        if (msg is not null) msg.IsExpanded = !msg.IsExpanded;
    }

    public void ClearMessages()
    {
        ClearUiMessages();
        _session.ClearHistory();
    }

    /// <summary>
    ///   Trim the UI message collection to MaxUiMessages, disposing
    ///   removed messages. The full conversation history remains in
    ///   the AgentSession for LLM context.
    /// </summary>
    private void TrimMessages()
    {
        while (Messages.Count > MaxUiMessages)
        {
            var old = Messages[0];
            old.Dispose();
            Messages.RemoveAt(0);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        UnsubscribeSessionEvents(_session);
        _session.Dispose();
        _cts?.Dispose();
        _cts = null;
        foreach (var msg in Messages)
            msg.Dispose();
    }
}

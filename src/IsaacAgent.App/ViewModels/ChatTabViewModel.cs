using System.Collections.ObjectModel;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.Agent;
using IsaacAgent.Agent.Engine;
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
    private readonly string _tabId = Guid.NewGuid().ToString("N")[..8];
    private AgentSession _session;
    private CancellationTokenSource? _cts;
    private string? _currentProjectDir;

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

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public ChatTabViewModel(IServiceProvider services, ILogger<ChatTabViewModel> logger, string? projectDir = null)
    {
        _services = services;
        _logger = logger;
        _sessionFactory = services.GetRequiredService<IAgentSessionFactory>();
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
        Messages.Clear();
        TotalInputTokens = 0;
        TotalOutputTokens = 0;
        _session.LoadHistory(GetHistoryPath(projectDir));
        RestoreMessagesFromHistory();
    }

    private string GetHistoryPath(string? projectDir)
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IsaacAgent", "history");
        if (string.IsNullOrEmpty(projectDir))
            return Path.Combine(baseDir, $"default_{_tabId}.json");

        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(projectDir.ToLowerInvariant()));
        var hash = Convert.ToHexString(hashBytes)[..12];
        return Path.Combine(baseDir, $"project_{hash}_{_tabId}.json");
    }

    private void RestoreMessagesFromHistory()
    {
        foreach (var msg in _session.History)
        {
            if (msg.Role is "system" or "tool" or "tool_result") continue;
            Messages.Add(new ChatMessageViewModel
            {
                Role = msg.Role,
                Content = msg.Content
            });
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsGenerating) return;

        var userMsg = InputText.Trim();
        InputText = "";

        _cts = new CancellationTokenSource();
        IsGenerating = true;

        Messages.Add(new ChatMessageViewModel { Role = "user", Content = userMsg });

        var assistantMsg = new ChatMessageViewModel { Role = "assistant", Content = "" };
        Messages.Add(assistantMsg);

        try
        {
            await foreach (var chunk in _session.SendMessageAsync(userMsg, _cts.Token))
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
            IsGenerating = false;
            _cts?.Dispose();
            _cts = null;
            var historyPath = GetHistoryPath(_currentProjectDir);
            _ = Task.Run(() => _session.SaveHistory(historyPath, CancellationToken.None), CancellationToken.None);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void ToggleExpand(ChatMessageViewModel? msg)
    {
        if (msg is not null) msg.IsExpanded = !msg.IsExpanded;
    }

    public void ClearMessages()
    {
        foreach (var msg in Messages)
            msg.Dispose();
        Messages.Clear();
        _session.ClearHistory();
        TotalInputTokens = 0;
        TotalOutputTokens = 0;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        UnsubscribeSessionEvents(_session);
        _cts?.Dispose();
        _cts = null;
        foreach (var msg in Messages)
            msg.Dispose();
    }
}

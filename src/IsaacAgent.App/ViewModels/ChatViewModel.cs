using System.Collections.ObjectModel;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.Agent.Engine;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.ViewModels;

public sealed partial class ChatViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ChatViewModel> _logger;
    private AgentSession _session;
    private CancellationTokenSource? _cts;
    private string? _currentProjectDir;

    [ObservableProperty]
    private string _inputText = "";

    [ObservableProperty]
    private bool _isGenerating;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public ChatViewModel(IServiceProvider services, ILogger<ChatViewModel> logger)
    {
        _services = services;
        _logger = logger;
        _session = services.GetRequiredService<AgentSession>();

        _session.OnToolCall += (name, args) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                Messages.Add(new ChatMessageViewModel
                {
                    Role = "tool",
                    Content = $"\U0001f527 {name}: {args}",
                    IsToolCall = true
                }));
        };

        _session.OnToolResult += (result) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                Messages.Add(new ChatMessageViewModel
                {
                    Role = "tool_result",
                    Content = result.Length > 500 ? result[..500] + "..." : result,
                    IsToolResult = true
                }));
        };

        _session.OnError += (err) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                Messages.Add(new ChatMessageViewModel
                {
                    Role = "error",
                    Content = $"Error: {err}"
                }));
        };
    }

    public void OnProjectChanged(string? projectDir)
    {
        _currentProjectDir = projectDir;
        _session.SetProjectDirectory(projectDir);
        Messages.Clear();
        _session.LoadHistory(GetHistoryPath(projectDir));
        RestoreMessagesFromHistory();
    }

    private static string GetHistoryPath(string? projectDir)
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IsaacAgent", "history");
        if (string.IsNullOrEmpty(projectDir))
            return Path.Combine(baseDir, "default.json");

        // Use SHA256 instead of GetHashCode — GetHashCode is randomized per-process
        // and would produce different file names across restarts.
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(projectDir.ToLowerInvariant()));
        var hash = Convert.ToHexString(hashBytes)[..12];
        return Path.Combine(baseDir, $"project_{hash}.json");
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

        try
        {
            var assistantMsg = new ChatMessageViewModel { Role = "assistant", Content = "" };
            Messages.Add(assistantMsg);

            await foreach (var chunk in _session.SendMessageAsync(userMsg, _cts.Token))
            {
                assistantMsg.Content += chunk;
            }
        }
        catch (OperationCanceledException)
        {
            Messages.Add(new ChatMessageViewModel { Role = "system", Content = "(cancelled)" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Send failed");
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
            _session.SaveHistory(GetHistoryPath(_currentProjectDir));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    public void ClearMessages()
    {
        Messages.Clear();
        _session.ClearHistory();
    }
}

public sealed partial class ChatMessageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _role = "";

    [ObservableProperty]
    private string _content = "";

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public bool IsToolCall { get; set; }
    public bool IsToolResult { get; set; }
    public bool IsError => Role == "error";
    public bool IsSystem => Role == "system";

    public string RoleLabel => Role switch
    {
        "user" => "You",
        "assistant" => "IsaacAgent",
        "tool" => "Tool Call",
        "tool_result" => "Tool Result",
        "error" => "Error",
        "system" => "System",
        _ => Role
    };

    public IBrush BackgroundBrush => Role switch
    {
        "user" => new SolidColorBrush(Color.Parse("#1e4d8b")),
        "assistant" => new SolidColorBrush(Color.Parse("#2d2d30")),
        "tool" => new SolidColorBrush(Color.Parse("#3d3520")),
        "tool_result" => new SolidColorBrush(Color.Parse("#1a3a1a")),
        "error" => new SolidColorBrush(Color.Parse("#5c1a1a")),
        "system" => new SolidColorBrush(Color.Parse("#3a3a3a")),
        _ => new SolidColorBrush(Color.Parse("#2d2d30"))
    };

    public HorizontalAlignment HorizontalAlign => Role == "user" ? HorizontalAlignment.Right : HorizontalAlignment.Left;
}

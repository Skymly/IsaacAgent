using System.Collections.ObjectModel;
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
    private AgentSession? _session;

    [ObservableProperty]
    private string _inputText = "";

    [ObservableProperty]
    private bool _isGenerating;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public ChatViewModel(IServiceProvider services, ILogger<ChatViewModel> logger)
    {
        _services = services;
        _logger = logger;
    }

    public void InitializeSession(string? projectDir)
    {
        var chat = _services.GetRequiredService<IChatService>();
        var tools = _services.GetRequiredService<ToolRegistry>();
        _session = new AgentSession(chat, tools, projectDir,
            _services.GetRequiredService<ILogger<AgentSession>>());

        _session.OnToolCall += (name, args) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                Messages.Add(new ChatMessageViewModel
                {
                    Role = "tool",
                    Content = $"🔧 {name}: {args}",
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
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsGenerating) return;
        if (_session is null) return;

        var userMsg = InputText.Trim();
        InputText = "";
        IsGenerating = true;

        Messages.Add(new ChatMessageViewModel { Role = "user", Content = userMsg });

        try
        {
            var assistantMsg = new ChatMessageViewModel { Role = "assistant", Content = "" };
            Messages.Add(assistantMsg);

            await foreach (var chunk in _session.SendMessageAsync(userMsg))
            {
                assistantMsg.Content += chunk;
            }
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
        }
    }

    public void ClearMessages()
    {
        Messages.Clear();
        _session?.ClearHistory();
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
}

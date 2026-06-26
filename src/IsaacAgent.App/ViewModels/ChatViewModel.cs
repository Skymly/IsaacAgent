using System.Collections.ObjectModel;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.Agent;
using IsaacAgent.Agent.Engine;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.ViewModels;

/// <summary>
/// Manages multiple chat tabs, each with an independent AgentSession.
/// The active tab is exposed via <see cref="ActiveTab"/> for binding.
/// </summary>
public sealed partial class ChatViewModel : ObservableObject, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ChatViewModel> _logger;
    private string? _currentProjectDir;

    public ObservableCollection<ChatTabViewModel> Tabs { get; } = [];

    [ObservableProperty]
    private ChatTabViewModel? _activeTab;

    public ChatViewModel(IServiceProvider services, ILogger<ChatViewModel> logger)
    {
        _services = services;
        _logger = logger;
        AddTab();
    }

    [RelayCommand]
    private void AddTab()
    {
        var tab = new ChatTabViewModel(_services,
            _services.GetRequiredService<ILogger<ChatTabViewModel>>(),
            _currentProjectDir);
        tab.Title = $"Chat {Tabs.Count + 1}";
        Tabs.Add(tab);
        SetActiveTab(tab);
    }

    [RelayCommand]
    private void CloseTab(ChatTabViewModel? tab)
    {
        if (tab is null || Tabs.Count <= 1) return; // Keep at least one tab

        var idx = Tabs.IndexOf(tab);
        if (idx < 0) return;
        tab.Dispose();
        Tabs.Remove(tab);

        if (ActiveTab == tab)
        {
            var newIdx = Math.Min(idx, Tabs.Count - 1);
            SetActiveTab(Tabs[newIdx]);
        }
    }

    [RelayCommand]
    private void SelectTab(ChatTabViewModel? tab)
    {
        if (tab is not null) SetActiveTab(tab);
    }

    private void SetActiveTab(ChatTabViewModel tab)
    {
        if (ActiveTab is not null) ActiveTab.IsActive = false;
        ActiveTab = tab;
        tab.IsActive = true;
    }

    public bool CanCloseTabs => Tabs.Count > 1;

    public void OnProjectChanged(string? projectDir)
    {
        _currentProjectDir = projectDir;
        foreach (var tab in Tabs)
            tab.OnProjectChanged(projectDir);
    }

    public void ClearMessages()
    {
        ActiveTab?.ClearMessages();
    }

    public void Dispose()
    {
        foreach (var tab in Tabs)
            tab.Dispose();
        Tabs.Clear();
    }
}

public sealed partial class ChatMessageViewModel : ObservableObject, IDisposable
{
    private static readonly SolidColorBrush _userBrush = new(Color.Parse("#1e4d8b"));
    private static readonly SolidColorBrush _assistantBrush = new(Color.Parse("#2d2d30"));
    private static readonly SolidColorBrush _toolBrush = new(Color.Parse("#3d3520"));
    private static readonly SolidColorBrush _toolResultBrush = new(Color.Parse("#1a3a1a"));
    private static readonly SolidColorBrush _retrievalBrush = new(Color.Parse("#1a2a3a"));
    private static readonly SolidColorBrush _errorBrush = new(Color.Parse("#5c1a1a"));
    private static readonly SolidColorBrush _systemBrush = new(Color.Parse("#3a3a3a"));
    private static readonly SolidColorBrush _defaultBrush = new(Color.Parse("#2d2d30"));

    [ObservableProperty]
    private string _role = "";

    [ObservableProperty]
    private string _content = "";

    [ObservableProperty]
    private string _toolName = "";

    [ObservableProperty]
    private TimeSpan _toolDuration;

    [ObservableProperty]
    private bool _isExpanded;

    private const int RenderDebounceMs = 150;

    private string _debouncedMarkdown = "";
    private bool _markdownInitialized;
    private readonly Avalonia.Threading.DispatcherTimer _renderTimer;

    public string DebouncedMarkdown => _debouncedMarkdown;

    partial void OnContentChanged(string value)
    {
        if (!_markdownInitialized)
        {
            _debouncedMarkdown = value;
            _markdownInitialized = true;
            OnPropertyChanged(nameof(DebouncedMarkdown));
            return;
        }

        _renderTimer.Stop();
        _renderTimer.Start();
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        _renderTimer.Stop();
        _debouncedMarkdown = Content;
        OnPropertyChanged(nameof(DebouncedMarkdown));
    }

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public bool IsToolCall { get; set; }
    public bool IsToolResult { get; set; }
    public bool IsError => Role == "error";
    public bool IsSystem => Role == "system";
    public bool IsTool => Role is "tool" or "tool_result";
    public bool IsRetrieval => Role == "retrieval";
    public bool IsRegular => !IsTool && !IsRetrieval;

    public string ToolDurationLabel =>
        ToolDuration.TotalSeconds < 1 ? $"{ToolDuration.TotalMilliseconds:F0}ms" : $"{ToolDuration.TotalSeconds:F1}s";

    public string ToolArgsPreview =>
        string.IsNullOrEmpty(Content) ? "" :
        Content.Length > 80 ? Content[..80] + "..." : Content;

    public ChatMessageViewModel()
    {
        _renderTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(RenderDebounceMs)
        };
        _renderTimer.Tick += OnRenderTick;
    }

    public string RoleLabel => Role switch
    {
        "user" => "You",
        "assistant" => "IsaacAgent",
        "tool" => $"🔧 {ToolName}",
        "tool_result" => $"✅ {ToolName} ({ToolDurationLabel})",
        "retrieval" => "📚 Knowledge",
        "error" => "Error",
        "system" => "System",
        _ => Role
    };

    public IBrush BackgroundBrush => Role switch
    {
        "user" => _userBrush,
        "assistant" => _assistantBrush,
        "tool" => _toolBrush,
        "tool_result" => _toolResultBrush,
        "retrieval" => _retrievalBrush,
        "error" => _errorBrush,
        "system" => _systemBrush,
        _ => _defaultBrush
    };

    public HorizontalAlignment HorizontalAlign => Role == "user" ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public void Dispose()
    {
        _renderTimer.Stop();
        _renderTimer.Tick -= OnRenderTick;
    }
}

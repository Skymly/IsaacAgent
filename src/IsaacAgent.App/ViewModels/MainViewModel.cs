using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IChatSessionStore _chatSessionStore;
    private readonly SemaphoreSlim _projectLoadGate = new(1, 1);
    private int _projectLoadVersion;
    private string? _previousProjectDir;

    public ChatViewModel Chat { get; }
    public ProjectViewModel Project { get; }
    public QuickReferenceViewModel QuickReference { get; }
    public LogMonitorService LogMonitor { get; }
    public ToastService Toasts { get; }
    public ChatHistoryService ChatHistory { get; }

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private bool _isSearchVisible;

    public MainViewModel(IServiceProvider services, ILogger<MainViewModel> logger)
    {
        _services = services;
        _logger = logger;
        Chat = services.GetRequiredService<ChatViewModel>();
        Project = services.GetRequiredService<ProjectViewModel>();
        QuickReference = services.GetRequiredService<QuickReferenceViewModel>();
        LogMonitor = services.GetRequiredService<LogMonitorService>();
        Toasts = services.GetRequiredService<ToastService>();
        ChatHistory = services.GetRequiredService<ChatHistoryService>();
        _chatSessionStore = services.GetRequiredService<IChatSessionStore>();

        StatusText = GetString("StatusReady");

        Project.ProjectLoaded += OnProjectLoadedAsync;
    }

    /// <summary>
    /// Saves the outgoing project via the Chat session store, then hydrates the
    /// incoming project's tabs and AgentSession envelopes from the store.
    /// Serialized with a generation token so overlapping opens cannot apply a
    /// stale manifest after a newer project path is already shown.
    /// </summary>
    private async Task OnProjectLoadedAsync(string? path)
    {
        var version = Interlocked.Increment(ref _projectLoadVersion);

        await _projectLoadGate.WaitAsync();
        try
        {
            if (version != Volatile.Read(ref _projectLoadVersion))
                return;

            if (!string.IsNullOrEmpty(_previousProjectDir))
            {
                var outgoing = Chat.BuildSessionManifest(_previousProjectDir);
                await _chatSessionStore.SaveAsync(_previousProjectDir, outgoing);
            }

            if (version != Volatile.Read(ref _projectLoadVersion))
                return;

            if (string.IsNullOrEmpty(path))
            {
                // No project open: do not use the store as a write target for orphan sessions.
                Chat.ApplySessionManifest(null, new ProjectSessionManifest());
                _previousProjectDir = null;
                StatusText = GetString("StatusNoProject");
                return;
            }

            var incoming = await _chatSessionStore.LoadAsync(path);

            if (version != Volatile.Read(ref _projectLoadVersion))
                return;

            Chat.ApplySessionManifest(path, incoming);
            _previousProjectDir = path;

            StatusText = $"Project: {Project.ProjectName}";
            Toasts.ShowSuccess($"{GetString("ToastProjectLoaded")}: {Project.ProjectName}");
        }
        finally
        {
            _projectLoadGate.Release();
        }
    }

    /// <summary>
    /// Flushes the current project’s Chat session store (app close / shutdown).
    /// No-op when no project is open.
    /// </summary>
    public Task FlushCurrentSessionAsync(CancellationToken ct = default) =>
        Chat.PersistSessionAsync(ct);

    /// <summary>
    ///   Look up a localized string from application resources.
    ///   Falls back to the key if not found.
    /// </summary>
    private static string GetString(string key)
    {
        if (Avalonia.Application.Current?.Resources.TryGetValue(key, out var v) == true && v is string s)
            return s;
        return key;
    }

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        await Project.CreateNewProjectCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        await Project.OpenProjectCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ClearChat()
    {
        Chat.ClearMessages();
        StatusText = GetString("StatusChatCleared");
    }

    [RelayCommand]
    private void ExportChatMarkdown()
    {
        if (Chat.ActiveTab is null) return;
        var markdown = ChatHistoryService.ExportToMarkdown(Chat.ActiveTab);
        var tabTitle = Chat.ActiveTab.Title ?? "chat";
        ExportToFile(markdown, $"{tabTitle}.md");
    }

    [RelayCommand]
    private void ExportChatJson()
    {
        if (Chat.ActiveTab is null) return;
        var json = ChatHistoryService.ExportToJson(Chat.ActiveTab);
        var tabTitle = Chat.ActiveTab.Title ?? "chat";
        ExportToFile(json, $"{tabTitle}.json");
    }

    private void ExportToFile(string content, string fileName)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "IsaacAgentExports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);
            File.WriteAllText(path, content);
            Toasts.ShowSuccess($"Exported to: {path}");
        }
        catch (Exception ex)
        {
            Toasts.ShowError($"Export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        if (!IsSearchVisible) SearchQuery = "";
    }

    partial void OnSearchQueryChanged(string value)
    {
        // Search results are computed on demand via SearchResults property.
        OnPropertyChanged(nameof(SearchResults));
    }

    /// <summary>
    ///   Search results for the current query across all chat tabs.
    /// </summary>
    public List<(string TabTitle, ChatMessageViewModel Message)> SearchResults =>
        ChatHistoryService.SearchMessages(Chat, SearchQuery);
}

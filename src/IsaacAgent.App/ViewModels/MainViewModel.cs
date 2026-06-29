using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.Agent.Engine;
using IsaacAgent.App.Services;
using IsaacAgent.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MainViewModel> _logger;

    public ChatViewModel Chat { get; }
    public ProjectViewModel Project { get; }
    public QuickReferenceViewModel QuickReference { get; }
    public LogMonitorService LogMonitor { get; }
    public ToastService Toasts { get; }

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _isBusy;

    public MainViewModel(IServiceProvider services, ILogger<MainViewModel> logger)
    {
        _services = services;
        _logger = logger;
        Chat = services.GetRequiredService<ChatViewModel>();
        Project = services.GetRequiredService<ProjectViewModel>();
        QuickReference = services.GetRequiredService<QuickReferenceViewModel>();
        LogMonitor = services.GetRequiredService<LogMonitorService>();
        Toasts = services.GetRequiredService<ToastService>();

        StatusText = GetString("StatusReady");

        Project.ProjectLoaded += path =>
        {
            Chat.OnProjectChanged(path);
            StatusText = string.IsNullOrEmpty(path)
                ? GetString("StatusNoProject")
                : $"Project: {Project.ProjectName}";
            if (!string.IsNullOrEmpty(path))
                Toasts.ShowSuccess($"{GetString("ToastProjectLoaded")}: {Project.ProjectName}");
        };
    }

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
}

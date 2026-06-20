using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.Agent.Engine;
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

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isBusy;

    public MainViewModel(IServiceProvider services, ILogger<MainViewModel> logger)
    {
        _services = services;
        _logger = logger;
        Chat = services.GetRequiredService<ChatViewModel>();
        Project = services.GetRequiredService<ProjectViewModel>();
    }

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        await Project.CreateNewProjectCommand.ExecuteAsync(null);
        StatusText = $"Project: {Project.ProjectName}";
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        await Project.OpenProjectCommand.ExecuteAsync(null);
        StatusText = $"Project: {Project.ProjectName}";
    }

    [RelayCommand]
    private void ClearChat()
    {
        Chat.ClearMessages();
        StatusText = "Chat cleared";
    }
}

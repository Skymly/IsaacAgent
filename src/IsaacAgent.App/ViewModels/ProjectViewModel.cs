using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.App.Services;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.ViewModels;

public sealed partial class ProjectViewModel : ObservableObject
{
    private readonly ILogger<ProjectViewModel> _logger;
    private readonly AppConfiguration _config;

    [ObservableProperty]
    private string _projectName = "(No project)";

    [ObservableProperty]
    private string _projectPath = "";

    [ObservableProperty]
    private bool _hasProject;

    public ObservableCollection<FileTreeItem> Files { get; } = [];

    public Func<Task<IStorageFolder?>>? PickFolderAsync { get; set; }

    public ProjectViewModel(ILogger<ProjectViewModel> logger, AppConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    [RelayCommand]
    private async Task CreateNewProjectAsync()
    {
        if (PickFolderAsync is null) return;

        var folder = await PickFolderAsync();
        if (folder is null) return;

        var path = folder.Path.LocalPath;
        _logger.LogInformation("Creating new project in {Path}", path);

        ProjectPath = path;
        ProjectName = Path.GetFileName(path);
        HasProject = true;
        RefreshFiles();
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        if (PickFolderAsync is null) return;

        var folder = await PickFolderAsync();
        if (folder is null) return;

        LoadProject(folder.Path.LocalPath);
    }

    public void LoadProject(string path)
    {
        if (!Directory.Exists(path)) return;

        ProjectPath = path;
        ProjectName = Path.GetFileName(path);
        HasProject = true;
        RefreshFiles();
    }

    private void RefreshFiles()
    {
        Files.Clear();
        if (string.IsNullOrEmpty(ProjectPath)) return;

        try
        {
            var files = Directory.GetFiles(ProjectPath, "*", SearchOption.AllDirectories);
            foreach (var file in files.OrderBy(f => f))
            {
                var relPath = Path.GetRelativePath(ProjectPath, file).Replace('\\', '/');
                Files.Add(new FileTreeItem
                {
                    Name = Path.GetFileName(file),
                    Path = relPath,
                    IsLua = file.EndsWith(".lua"),
                    IsXml = file.EndsWith(".xml")
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh files");
        }
    }
}

public sealed partial class FileTreeItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _path = "";

    public bool IsLua { get; set; }
    public bool IsXml { get; set; }
}

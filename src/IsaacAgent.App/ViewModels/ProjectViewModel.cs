using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.App.Services;
using IsaacAgent.Tools.Implementations;
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

    [ObservableProperty]
    private string _filePreviewContent = "";

    [ObservableProperty]
    private string _filePreviewName = "";

    [ObservableProperty]
    private bool _hasFilePreview;

    public ObservableCollection<FileTreeItem> Files { get; } = [];

    public Func<Task<IStorageFolder?>>? PickFolderAsync { get; set; }

    public event Action<string?>? ProjectLoaded;

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

        var projectDir = folder.Path.LocalPath;
        var modName = Path.GetFileName(projectDir);

        try
        {
            Directory.CreateDirectory(projectDir);
            var scaffold = new ScaffoldModTool(projectDir);
            var args = System.Text.Json.JsonSerializer.Serialize(new { name = modName });
            await scaffold.ExecuteAsync(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scaffold new project at {Path}", projectDir);
        }

        LoadProject(projectDir);
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        if (PickFolderAsync is null) return;
        var folder = await PickFolderAsync();
        if (folder is null) return;
        LoadProject(folder.Path.LocalPath);
    }

    [RelayCommand]
    private async Task OpenFileAsync(FileTreeItem? item)
    {
        if (item is null || string.IsNullOrEmpty(ProjectPath)) return;

        var fullPath = Path.Combine(ProjectPath, item.Path);
        if (!File.Exists(fullPath)) return;

        try
        {
            var content = await File.ReadAllTextAsync(fullPath);
            FilePreviewName = item.Path;
            FilePreviewContent = content;
            HasFilePreview = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file {Path}", item.Path);
        }
    }

    public void LoadProject(string path)
    {
        if (!Directory.Exists(path)) return;
        ProjectPath = path;
        ProjectName = Path.GetFileName(path);
        HasProject = true;
        HasFilePreview = false;
        FilePreviewContent = "";
        FilePreviewName = "";
        RefreshFiles();
        ProjectLoaded?.Invoke(path);
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

using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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

        await LoadProjectAsync(projectDir);
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        if (PickFolderAsync is null) return;
        var folder = await PickFolderAsync();
        if (folder is null) return;
        await LoadProjectAsync(folder.Path.LocalPath);
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

    public async Task LoadProjectAsync(string path)
    {
        if (!Directory.Exists(path)) return;
        ProjectPath = path;
        ProjectName = Path.GetFileName(path);
        HasProject = true;
        HasFilePreview = false;
        FilePreviewContent = "";
        FilePreviewName = "";
        await RefreshFilesAsync();
        ProjectLoaded?.Invoke(path);
    }

    private Task RefreshFilesAsync()
    {
        Files.Clear();
        if (string.IsNullOrEmpty(ProjectPath)) return Task.CompletedTask;

        var projectPath = ProjectPath;
        try
        {
            BuildFileTree(projectPath, projectPath, Files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh files");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    ///   Recursively populates <paramref name="target"/> with directory and
    ///   file nodes. Directories are listed first (sorted), then files
    ///   (sorted). Common build artifacts (.git, bin, obj) are skipped.
    /// </summary>
    private static void BuildFileTree(string projectRoot, string currentDir, ObservableCollection<FileTreeItem> target)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", "bin", "obj", ".vs", "node_modules"
        };

        // Directories first
        foreach (var dir in Directory.GetDirectories(currentDir)
                     .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var dirName = Path.GetFileName(dir);
            if (excluded.Contains(dirName)) continue;

            var relPath = Path.GetRelativePath(projectRoot, dir).Replace('\\', '/');
            var dirItem = new FileTreeItem
            {
                Name = dirName,
                Path = relPath,
                IsDirectory = true,
                IsExpanded = true // expand top-level by default
            };
            BuildFileTree(projectRoot, dir, dirItem.Children);
            // Skip empty directories (no files anywhere beneath)
            if (!dirItem.Children.Any())
                continue;
            target.Add(dirItem);
        }

        // Then files
        foreach (var file in Directory.GetFiles(currentDir)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(file);
            var relPath = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
            target.Add(new FileTreeItem
            {
                Name = fileName,
                Path = relPath,
                IsLua = file.EndsWith(".lua", StringComparison.OrdinalIgnoreCase),
                IsXml = file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            });
        }
    }
}

public sealed partial class FileTreeItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _path = "";

    /// <summary>
    ///   Relative path from the project root, using forward slashes.
    ///   For directories this is the directory path; for files it is
    ///   the full file path.
    /// </summary>
    public bool IsDirectory { get; set; }

    public bool IsLua { get; set; }
    public bool IsXml { get; set; }

    /// <summary>
    ///   Whether this directory node is expanded in the TreeView.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<FileTreeItem> Children { get; } = [];

    /// <summary>
    ///   Flattens this node and all descendants into a list of file items
    ///   (excluding directories). Useful for tests and searching.
    /// </summary>
    public IEnumerable<FileTreeItem> FlattenFiles()
    {
        if (!IsDirectory)
        {
            yield return this;
            yield break;
        }
        foreach (var child in Children)
            foreach (var f in child.FlattenFiles())
                yield return f;
    }
}

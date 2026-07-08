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

    [ObservableProperty]
    private string _fileSearchText = "";

    [ObservableProperty]
    private bool _hasFileSearch;

    public ObservableCollection<FileTreeItem> Files { get; } = [];

    /// <summary>Filtered file list for search results.</summary>
    public ObservableCollection<FileTreeItem> FilteredFiles { get; } = [];

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

        // Clear previous preview to free memory before loading new content
        FilePreviewContent = "";

        try
        {
            // Limit preview to first 100KB to avoid memory issues with large files
            const int MaxPreviewBytes = 100 * 1024;
            var fileInfo = new FileInfo(fullPath);
            string content;
            if (fileInfo.Length > MaxPreviewBytes)
            {
                await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                using var reader = new StreamReader(stream);
                var buffer = new char[MaxPreviewBytes];
                var read = await reader.ReadAsync(buffer);
                content = new string(buffer, 0, read) + "\n\n... (truncated, file is too large)";
            }
            else
            {
                content = await File.ReadAllTextAsync(fullPath);
            }
            FilePreviewName = item.Path;
            FilePreviewContent = content;
            HasFilePreview = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file {Path}", item.Path);
        }
    }

    /// <summary>
    ///   Clear the file preview to free memory.
    /// </summary>
    public void ClearFilePreview()
    {
        FilePreviewContent = "";
        FilePreviewName = "";
        HasFilePreview = false;
    }

    /// <summary>
    ///   Create a new file in the project. If a directory is selected,
    ///   the file is created inside it; otherwise at the project root.
    /// </summary>
    [RelayCommand]
    private async Task CreateNewFileAsync(FileTreeItem? targetDir)
    {
        if (string.IsNullOrEmpty(ProjectPath)) return;

        var dirPath = targetDir is { IsDirectory: true }
            ? Path.Combine(ProjectPath, targetDir.Path)
            : ProjectPath;

        var fileName = $"newfile_{DateTime.Now:HHmmss}.lua";
        var fullPath = Path.Combine(dirPath, fileName);
        try
        {
            await File.WriteAllTextAsync(fullPath, "-- New file\n");
            await RefreshFilesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create file {Path}", fullPath);
        }
    }

    /// <summary>
    ///   Create a new folder in the project.
    /// </summary>
    [RelayCommand]
    private async Task CreateNewFolderAsync(FileTreeItem? targetDir)
    {
        if (string.IsNullOrEmpty(ProjectPath)) return;

        var dirPath = targetDir is { IsDirectory: true }
            ? Path.Combine(ProjectPath, targetDir.Path)
            : ProjectPath;

        var folderName = $"newfolder_{DateTime.Now:HHmmss}";
        var fullPath = Path.Combine(dirPath, folderName);
        try
        {
            Directory.CreateDirectory(fullPath);
            await RefreshFilesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create folder {Path}", fullPath);
        }
    }

    /// <summary>
    ///   Delete a file or folder from the project.
    /// </summary>
    [RelayCommand]
    private async Task DeleteFileAsync(FileTreeItem? item)
    {
        if (item is null || string.IsNullOrEmpty(ProjectPath)) return;

        var fullPath = Path.Combine(ProjectPath, item.Path);
        try
        {
            if (item.IsDirectory)
                Directory.Delete(fullPath, recursive: true);
            else
                File.Delete(fullPath);
            await RefreshFilesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {Path}", fullPath);
        }
    }

    /// <summary>
    ///   Rename a file or folder.
    /// </summary>
    [RelayCommand]
    private async Task RenameFileAsync(FileTreeItem? item)
    {
        if (item is null || string.IsNullOrEmpty(ProjectPath)) return;

        var fullPath = Path.Combine(ProjectPath, item.Path);
        var dir = Path.GetDirectoryName(fullPath) ?? ProjectPath;
        var newName = item.Name + "_renamed";
        var newPath = Path.Combine(dir, newName);

        try
        {
            if (item.IsDirectory)
                Directory.Move(fullPath, newPath);
            else
                File.Move(fullPath, newPath);
            await RefreshFilesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename {Path}", fullPath);
        }
    }

    /// <summary>
    ///   Copy the full path of a file or folder to the clipboard.
    /// </summary>
    [RelayCommand]
    private async Task CopyPathAsync(FileTreeItem? item)
    {
        if (item is null || string.IsNullOrEmpty(ProjectPath)) return;
        var fullPath = Path.Combine(ProjectPath, item.Path);

        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var clipboard = desktop.MainWindow?.Clipboard;
            if (clipboard is not null)
                await clipboard.SetTextAsync(fullPath);
        }
    }

    /// <summary>
    ///   Open the containing folder in the system file explorer.
    /// </summary>
    [RelayCommand]
    private void OpenInExplorer(FileTreeItem? item)
    {
        if (string.IsNullOrEmpty(ProjectPath)) return;

        var targetPath = item is null
            ? ProjectPath
            : Path.Combine(ProjectPath, item.Path);

        var dirToOpen = item is { IsDirectory: true }
            ? targetPath
            : Path.GetDirectoryName(targetPath) ?? ProjectPath;

        try
        {
            // Cross-platform: use Process.Start with useShellExecute
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dirToOpen,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open explorer for {Path}", dirToOpen);
        }
    }

    /// <summary>
    ///   Open a file in the system default editor.
    /// </summary>
    [RelayCommand]
    private void OpenInExternalEditor(FileTreeItem? item)
    {
        if (item is null || item.IsDirectory || string.IsNullOrEmpty(ProjectPath)) return;

        var fullPath = Path.Combine(ProjectPath, item.Path);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open {Path} in external editor", fullPath);
        }
    }

    partial void OnFileSearchTextChanged(string value)
    {
        UpdateFilteredFiles();
    }

    /// <summary>
    ///   Update the filtered file list based on the search query.
    /// </summary>
    public void UpdateFilteredFiles()
    {
        FilteredFiles.Clear();
        HasFileSearch = !string.IsNullOrWhiteSpace(FileSearchText);

        if (!HasFileSearch) return;

        var query = FileSearchText.ToLowerInvariant();
        foreach (var file in FlattenAllFiles(Files))
        {
            if (file.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                file.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredFiles.Add(file);
            }
        }
    }

    private static IEnumerable<FileTreeItem> FlattenAllFiles(
        IEnumerable<FileTreeItem> items)
    {
        foreach (var item in items)
        {
            if (item.IsDirectory)
            {
                foreach (var child in FlattenAllFiles(item.Children))
                    yield return child;
            }
            else
            {
                yield return item;
            }
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

    private async Task RefreshFilesAsync()
    {
        if (string.IsNullOrEmpty(ProjectPath))
        {
            if (Dispatcher.UIThread.CheckAccess())
                Files.Clear();
            else
                await Dispatcher.UIThread.InvokeAsync(Files.Clear);
            return;
        }

        var projectPath = ProjectPath;
        try
        {
            var items = await Task.Run(() => BuildFileTreeSync(projectPath, projectPath));
            if (Dispatcher.UIThread.CheckAccess())
                ApplyFileTree(items);
            else
                await Dispatcher.UIThread.InvokeAsync(() => ApplyFileTree(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh files");
        }
    }

    private void ApplyFileTree(IReadOnlyList<FileTreeItem> items)
    {
        Files.Clear();
        foreach (var item in items)
            Files.Add(item);
    }

    /// <summary>
    ///   Synchronous version of BuildFileTree that returns a list instead
    ///   of populating an ObservableCollection. Safe to call on background
    ///   thread since it doesn't touch UI-bound collections.
    /// </summary>
    private static List<FileTreeItem> BuildFileTreeSync(string projectRoot, string currentDir)
    {
        var result = new List<FileTreeItem>();
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", "bin", "obj", ".vs", "node_modules"
        };

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
                IsExpanded = true
            };
            var children = BuildFileTreeSync(projectRoot, dir);
            foreach (var child in children)
                dirItem.Children.Add(child);
            if (dirItem.Children.Count > 0)
                result.Add(dirItem);
        }

        foreach (var file in Directory.GetFiles(currentDir)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(file);
            var relPath = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
            result.Add(new FileTreeItem
            {
                Name = fileName,
                Path = relPath,
                IsLua = file.EndsWith(".lua", StringComparison.OrdinalIgnoreCase),
                IsXml = file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            });
        }

        return result;
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

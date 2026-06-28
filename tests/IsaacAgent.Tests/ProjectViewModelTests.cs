using System.Collections.ObjectModel;
using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

[Collection("Avalonia")]
public class ProjectViewModelTests
{

    private static ProjectViewModel CreateViewModel()
    {
        var logger = Mock.Of<ILogger<ProjectViewModel>>();
        var config = new AppConfiguration();
        return new ProjectViewModel(logger, config);
    }

    /// <summary>
    ///   Flattens the tree into all file items (excluding directories).
    /// </summary>
    private static List<FileTreeItem> FlattenFiles(ObservableCollection<FileTreeItem> tree)
    {
        var result = new List<FileTreeItem>();
        foreach (var item in tree)
            result.AddRange(item.FlattenFiles());
        return result;
    }

    [Fact]
    public async Task LoadProject_ValidDirectory_SetsPropertiesAndLoadsFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_pvm_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "main.lua"), "local mod = RegisterMod('Test', 1)");
            File.WriteAllText(Path.Combine(tempDir, "metadata.xml"), "<metadata/>");
            Directory.CreateDirectory(Path.Combine(tempDir, "scripts"));
            File.WriteAllText(Path.Combine(tempDir, "scripts", "utils.lua"), "local M = {}");

            var vm = CreateViewModel();
            string? loadedPath = null;
            vm.ProjectLoaded += path => loadedPath = path;

            await vm.LoadProjectAsync(tempDir);

            Assert.Equal(tempDir, vm.ProjectPath);
            Assert.Equal(Path.GetFileName(tempDir), vm.ProjectName);
            Assert.True(vm.HasProject);
            Assert.Equal(tempDir, loadedPath);

            // Tree should have top-level files + a scripts directory node
            var allFiles = FlattenFiles(vm.Files);
            Assert.True(allFiles.Count >= 3);
            Assert.Contains(allFiles, f => f.Name == "main.lua" && f.IsLua);
            Assert.Contains(allFiles, f => f.Name == "metadata.xml" && f.IsXml);
            Assert.Contains(allFiles, f => f.Path == "scripts/utils.lua" && f.IsLua);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadProject_NonexistentDirectory_DoesNothing()
    {
        var vm = CreateViewModel();
        var fakePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}");

        await vm.LoadProjectAsync(fakePath);

        Assert.False(vm.HasProject);
        Assert.Empty(vm.Files);
    }

    [Fact]
    public async Task LoadProject_EmptyDirectory_LoadsNoFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_pvm_empty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = CreateViewModel();

            await vm.LoadProjectAsync(tempDir);

            Assert.True(vm.HasProject);
            Assert.Empty(vm.Files);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadProject_TreeStructure_DirectoriesComeBeforeFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_pvm_tree_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "scripts"));
            Directory.CreateDirectory(Path.Combine(tempDir, "resources"));
            File.WriteAllText(Path.Combine(tempDir, "main.lua"), "");
            File.WriteAllText(Path.Combine(tempDir, "scripts", "a.lua"), "");
            File.WriteAllText(Path.Combine(tempDir, "resources", "gfx.png"), "");

            var vm = CreateViewModel();
            await vm.LoadProjectAsync(tempDir);

            // Top-level: directories first, then files
            var dirs = vm.Files.Where(f => f.IsDirectory).ToList();
            var files = vm.Files.Where(f => !f.IsDirectory).ToList();
            Assert.True(dirs.Count >= 2, "Should have at least 2 directories");
            Assert.Single(files);
            Assert.Equal("main.lua", files[0].Name);
            Assert.Contains(dirs, d => d.Name == "scripts");
            Assert.Contains(dirs, d => d.Name == "resources");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadProject_TreeStructure_NestedDirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_pvm_nested_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "scripts", "subdir"));
            File.WriteAllText(Path.Combine(tempDir, "scripts", "subdir", "deep.lua"), "");

            var vm = CreateViewModel();
            await vm.LoadProjectAsync(tempDir);

            // scripts/ should contain subdir/ which contains deep.lua
            var scripts = vm.Files.FirstOrDefault(f => f.Name == "scripts");
            Assert.NotNull(scripts);
            Assert.True(scripts.IsDirectory);
            var subdir = scripts.Children.FirstOrDefault(f => f.Name == "subdir");
            Assert.NotNull(subdir);
            Assert.True(subdir.IsDirectory);
            var deepFile = subdir.Children.FirstOrDefault(f => f.Name == "deep.lua");
            Assert.NotNull(deepFile);
            Assert.True(deepFile.IsLua);
            Assert.Equal("scripts/subdir/deep.lua", deepFile.Path);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadProject_TreeStructure_ExcludesBuildArtifacts()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_pvm_excl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "bin"));
            Directory.CreateDirectory(Path.Combine(tempDir, "obj"));
            Directory.CreateDirectory(Path.Combine(tempDir, ".git"));
            File.WriteAllText(Path.Combine(tempDir, "bin", "output.lua"), "");
            File.WriteAllText(Path.Combine(tempDir, "obj", "temp.lua"), "");
            File.WriteAllText(Path.Combine(tempDir, ".git", "config"), "");
            File.WriteAllText(Path.Combine(tempDir, "main.lua"), "");

            var vm = CreateViewModel();
            await vm.LoadProjectAsync(tempDir);

            var allNames = new List<string>();
            CollectNames(vm.Files, allNames);
            Assert.Contains("main.lua", allNames);
            Assert.DoesNotContain("bin", allNames);
            Assert.DoesNotContain("obj", allNames);
            Assert.DoesNotContain(".git", allNames);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadProject_TreeStructure_EmptyDirectoriesAreSkipped()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_pvm_emptydir_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "emptydir"));
            File.WriteAllText(Path.Combine(tempDir, "main.lua"), "");

            var vm = CreateViewModel();
            await vm.LoadProjectAsync(tempDir);

            // emptydir has no files, so it should not appear
            Assert.DoesNotContain(vm.Files, f => f.Name == "emptydir");
            Assert.Contains(vm.Files, f => f.Name == "main.lua");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadProject_TreeStructure_TopLevelDirectoriesExpandedByDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_pvm_exp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "scripts"));
            File.WriteAllText(Path.Combine(tempDir, "scripts", "a.lua"), "");

            var vm = CreateViewModel();
            await vm.LoadProjectAsync(tempDir);

            var scripts = vm.Files.FirstOrDefault(f => f.Name == "scripts");
            Assert.NotNull(scripts);
            Assert.True(scripts.IsExpanded);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    private static void CollectNames(IEnumerable<FileTreeItem> items, List<string> names)
    {
        foreach (var item in items)
        {
            names.Add(item.Name);
            CollectNames(item.Children, names);
        }
    }

    [Fact]
    public async Task OpenFileAsync_ValidFile_SetsPreview()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_pvm_open_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var content = "local mod = RegisterMod('Test', 1)";
            var filePath = Path.Combine(tempDir, "main.lua");
            await File.WriteAllTextAsync(filePath, content);

            var vm = CreateViewModel();
            await vm.LoadProjectAsync(tempDir);

            var item = FlattenFiles(vm.Files).First(f => f.Name == "main.lua");
            await vm.OpenFileCommand.ExecuteAsync(item);

            Assert.True(vm.HasFilePreview);
            Assert.Equal("main.lua", vm.FilePreviewName);
            Assert.Equal(content, vm.FilePreviewContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task OpenFileAsync_NullItem_DoesNothing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_pvm_null_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(tempDir);

            await vm.OpenFileCommand.ExecuteAsync(null);

            Assert.False(vm.HasFilePreview);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task OpenFileAsync_DirectoryItem_DoesNothing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_pvm_dir_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "scripts"));
            File.WriteAllText(Path.Combine(tempDir, "scripts", "a.lua"), "");
            File.WriteAllText(Path.Combine(tempDir, "main.lua"), "");

            var vm = CreateViewModel();
            await vm.LoadProjectAsync(tempDir);

            var dirItem = vm.Files.First(f => f.IsDirectory);
            await vm.OpenFileCommand.ExecuteAsync(dirItem);

            // Opening a directory should not set file preview
            Assert.False(vm.HasFilePreview);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FileTreeItem_Properties_SetCorrectly()
    {
        var item = new FileTreeItem
        {
            Name = "test.lua",
            Path = "scripts/test.lua",
            IsLua = true,
            IsXml = false
        };

        Assert.Equal("test.lua", item.Name);
        Assert.Equal("scripts/test.lua", item.Path);
        Assert.True(item.IsLua);
        Assert.False(item.IsXml);
    }

    [Fact]
    public void FileTreeItem_DirectoryItem_HasChildrenCollection()
    {
        var dir = new FileTreeItem
        {
            Name = "scripts",
            Path = "scripts",
            IsDirectory = true
        };
        var file = new FileTreeItem
        {
            Name = "a.lua",
            Path = "scripts/a.lua",
            IsLua = true
        };
        dir.Children.Add(file);

        Assert.True(dir.IsDirectory);
        Assert.Single(dir.Children);
        Assert.Same(file, dir.Children[0]);
    }

    [Fact]
    public void FileTreeItem_FlattenFiles_FileReturnsSelf()
    {
        var file = new FileTreeItem { Name = "a.lua", Path = "a.lua", IsLua = true };
        var result = file.FlattenFiles().ToList();
        Assert.Single(result);
        Assert.Same(file, result[0]);
    }

    [Fact]
    public void FileTreeItem_FlattenFiles_DirectoryReturnsAllDescendants()
    {
        var root = new FileTreeItem { Name = "scripts", Path = "scripts", IsDirectory = true };
        var sub = new FileTreeItem { Name = "sub", Path = "scripts/sub", IsDirectory = true };
        var f1 = new FileTreeItem { Name = "a.lua", Path = "scripts/a.lua", IsLua = true };
        var f2 = new FileTreeItem { Name = "b.lua", Path = "scripts/sub/b.lua", IsLua = true };
        root.Children.Add(f1);
        root.Children.Add(sub);
        sub.Children.Add(f2);

        var result = root.FlattenFiles().ToList();
        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.Name == "a.lua");
        Assert.Contains(result, f => f.Name == "b.lua");
    }
}

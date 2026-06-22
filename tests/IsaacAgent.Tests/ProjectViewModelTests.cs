using Avalonia.Headless;
using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

public class ProjectViewModelTests
{
    private static ProjectViewModel CreateViewModel()
    {
        var logger = Mock.Of<ILogger<ProjectViewModel>>();
        var config = new AppConfiguration();
        return new ProjectViewModel(logger, config);
    }

    [Fact]
    public void LoadProject_ValidDirectory_SetsPropertiesAndLoadsFiles()
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

            vm.LoadProject(tempDir);

            Assert.Equal(tempDir, vm.ProjectPath);
            Assert.Equal(Path.GetFileName(tempDir), vm.ProjectName);
            Assert.True(vm.HasProject);
            Assert.Equal(tempDir, loadedPath);

            // Files should be loaded (sorted)
            Assert.True(vm.Files.Count >= 3);
            Assert.Contains(vm.Files, f => f.Name == "main.lua" && f.IsLua);
            Assert.Contains(vm.Files, f => f.Name == "metadata.xml" && f.IsXml);
            Assert.Contains(vm.Files, f => f.Path == "scripts/utils.lua" && f.IsLua);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadProject_NonexistentDirectory_DoesNothing()
    {
        var vm = CreateViewModel();
        var fakePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}");

        vm.LoadProject(fakePath);

        Assert.False(vm.HasProject);
        Assert.Empty(vm.Files);
    }

    [Fact]
    public void LoadProject_EmptyDirectory_LoadsNoFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_pvm_empty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = CreateViewModel();

            vm.LoadProject(tempDir);

            Assert.True(vm.HasProject);
            Assert.Empty(vm.Files);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
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
            vm.LoadProject(tempDir);

            var item = vm.Files.First(f => f.Name == "main.lua");
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
            vm.LoadProject(tempDir);

            await vm.OpenFileCommand.ExecuteAsync(null);

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
}

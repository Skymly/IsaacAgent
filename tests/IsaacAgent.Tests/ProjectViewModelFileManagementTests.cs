using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for ProjectViewModel file management commands —
///   create, delete, rename, search, and path operations.
/// </summary>
public class ProjectViewModelFileManagementTests
{
    private static ProjectViewModel CreateViewModel()
    {
        var logger = Mock.Of<ILogger<ProjectViewModel>>();
        var config = new AppConfiguration();
        return new ProjectViewModel(logger, config);
    }

    private static string CreateTempProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"isaac_proj_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "subfolder"));
        File.WriteAllText(Path.Combine(dir, "main.lua"), "-- main");
        File.WriteAllText(Path.Combine(dir, "subfolder", "helper.lua"), "-- helper");
        return dir;
    }

    [Fact]
    public async Task CreateNewFile_CreatesFileInProjectRoot()
    {
        var dir = CreateTempProject();
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(dir);

            await vm.CreateNewFileCommand.ExecuteAsync(null);

            // Verify a new file was created
            var files = Directory.GetFiles(dir);
            Assert.Contains(files, f => f.EndsWith(".lua"));
            Assert.True(files.Length >= 2); // main.lua + new file
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task CreateNewFolder_CreatesFolderInProjectRoot()
    {
        var dir = CreateTempProject();
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(dir);

            await vm.CreateNewFolderCommand.ExecuteAsync(null);

            var dirs = Directory.GetDirectories(dir);
            Assert.Contains(dirs, d => d.Contains("newfolder_"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DeleteFile_RemovesFileFromDisk()
    {
        var dir = CreateTempProject();
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(dir);

            var fileItem = vm.Files.First(f => !f.IsDirectory && f.Name == "main.lua");
            await vm.DeleteFileCommand.ExecuteAsync(fileItem);

            Assert.False(File.Exists(Path.Combine(dir, "main.lua")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DeleteFile_RemovesFolderFromDisk()
    {
        var dir = CreateTempProject();
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(dir);

            var folderItem = vm.Files.First(f => f.IsDirectory && f.Name == "subfolder");
            await vm.DeleteFileCommand.ExecuteAsync(folderItem);

            Assert.False(Directory.Exists(Path.Combine(dir, "subfolder")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task RenameFile_RenamesOnDisk()
    {
        var dir = CreateTempProject();
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(dir);

            var fileItem = vm.Files.First(f => !f.IsDirectory && f.Name == "main.lua");
            await vm.RenameFileCommand.ExecuteAsync(fileItem);

            // The renamed file should exist
            Assert.True(File.Exists(Path.Combine(dir, "main.lua_renamed")));
            Assert.False(File.Exists(Path.Combine(dir, "main.lua")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task FileSearchText_FilteredFilesContainsMatches()
    {
        var dir = CreateTempProject();
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(dir);

            vm.FileSearchText = "helper";

            Assert.True(vm.HasFileSearch);
            Assert.NotEmpty(vm.FilteredFiles);
            Assert.Contains(vm.FilteredFiles, f => f.Name == "helper.lua");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task FileSearchText_Empty_ClearsFilteredFiles()
    {
        var dir = CreateTempProject();
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(dir);

            vm.FileSearchText = "main";
            Assert.NotEmpty(vm.FilteredFiles);

            vm.FileSearchText = "";
            Assert.False(vm.HasFileSearch);
            Assert.Empty(vm.FilteredFiles);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task FileSearchText_NoMatch_EmptyFilteredFiles()
    {
        var dir = CreateTempProject();
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(dir);

            vm.FileSearchText = "nonexistent_file_xyz";
            Assert.True(vm.HasFileSearch);
            Assert.Empty(vm.FilteredFiles);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task FileSearchText_CaseInsensitive()
    {
        var dir = CreateTempProject();
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(dir);

            vm.FileSearchText = "MAIN";
            Assert.NotEmpty(vm.FilteredFiles);
            Assert.Contains(vm.FilteredFiles, f => f.Name == "main.lua");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DeleteFile_NullParameter_DoesNothing()
    {
        var dir = CreateTempProject();
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(dir);

            await vm.DeleteFileCommand.ExecuteAsync(null);

            // Project should be unchanged
            Assert.True(File.Exists(Path.Combine(dir, "main.lua")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task RenameFile_NullParameter_DoesNothing()
    {
        var dir = CreateTempProject();
        try
        {
            var vm = CreateViewModel();
            await vm.LoadProjectAsync(dir);

            await vm.RenameFileCommand.ExecuteAsync(null);

            Assert.True(File.Exists(Path.Combine(dir, "main.lua")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}

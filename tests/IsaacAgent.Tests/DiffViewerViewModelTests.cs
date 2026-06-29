using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for DiffViewerViewModel — project dir management,
///   refresh command, and selected file handling.
/// </summary>
[Collection("Avalonia")]
public class DiffViewerViewModelTests
{
    private static DiffViewerViewModel CreateViewModel()
    {
        var logger = Mock.Of<ILogger<DiffService>>();
        var diffService = new DiffService(logger);
        return new DiffViewerViewModel(diffService);
    }

    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        var vm = CreateViewModel();
        Assert.Null(vm.SelectedFile);
        Assert.NotNull(vm.DiffService);
    }

    [Fact]
    public void SetProjectDir_DoesNotThrow()
    {
        var vm = CreateViewModel();
        vm.SetProjectDir("/some/project");
        // No public getter for _projectDir, but RefreshAsync will use it
    }

    [Fact]
    public void SetProjectDir_Null_DoesNotThrow()
    {
        var vm = CreateViewModel();
        vm.SetProjectDir(null);
    }

    [Fact]
    public async Task RefreshAsync_NoProjectDir_SetsStatusText()
    {
        var vm = CreateViewModel();
        // No project dir set — should return early without calling git
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("No project open", vm.DiffService.StatusText);
    }

    // Note: RefreshAsync with a real project dir calls git diff as a
    // subprocess, which can hang in test environments. Those integration
    // scenarios are covered by DiffServiceTests with mocked process output.

    [Fact]
    public void SelectedFile_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        var file = new IsaacAgent.App.Services.DiffFile
        {
            FilePath = "main.lua",
            OldPath = "main.lua"
        };
        vm.SelectedFile = file;
        Assert.Same(file, vm.SelectedFile);
    }

    [Fact]
    public void SelectedFile_SetToNull_DoesNotThrow()
    {
        var vm = CreateViewModel();
        vm.SelectedFile = null;
        Assert.Null(vm.SelectedFile);
    }

    [Fact]
    public void DiffService_ExposedAsProperty()
    {
        var vm = CreateViewModel();
        Assert.NotNull(vm.DiffService);
        Assert.IsType<DiffService>(vm.DiffService);
    }
}

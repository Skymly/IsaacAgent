using IsaacAgent.App.ViewModels;
using IsaacAgent.Core.Models;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for TemplateGalleryViewModel — template selection,
///   validation, and scaffolding with placeholder substitution.
/// </summary>
public class TemplateGalleryViewModelTests
{
    [Fact]
    public void Constructor_LoadsAllTemplates()
    {
        var vm = new TemplateGalleryViewModel();
        Assert.NotEmpty(vm.Templates);
        Assert.Equal(ModTemplates.All.Count, vm.Templates.Count);
    }

    [Fact]
    public void Constructor_Defaults_AreEmpty()
    {
        var vm = new TemplateGalleryViewModel();
        Assert.Null(vm.SelectedTemplate);
        Assert.Equal("", vm.ModName);
        Assert.Equal("", vm.ModDescription);
        Assert.Equal("", vm.ModAuthor);
        Assert.Equal("", vm.StatusMessage);
    }

    [Fact]
    public async Task ScaffoldAsync_NoTemplate_SetsStatusMessage()
    {
        var vm = new TemplateGalleryViewModel();
        await vm.ScaffoldCommand.ExecuteAsync(null);
        Assert.Equal("Please select a template.", vm.StatusMessage);
    }

    [Fact]
    public async Task ScaffoldAsync_NoModName_SetsStatusMessage()
    {
        var vm = new TemplateGalleryViewModel();
        vm.SelectedTemplate = vm.Templates[0];
        await vm.ScaffoldCommand.ExecuteAsync(null);
        Assert.Equal("Please enter a mod name.", vm.StatusMessage);
    }

    [Fact]
    public async Task ScaffoldAsync_ValidInput_InvokesScaffoldRequested()
    {
        var vm = new TemplateGalleryViewModel();
        vm.SelectedTemplate = vm.Templates[0];
        vm.ModName = "TestMod";
        var wasCalled = false;
        vm.ScaffoldRequested = () =>
        {
            wasCalled = true;
            return Task.CompletedTask;
        };

        await vm.ScaffoldCommand.ExecuteAsync(null);
        Assert.True(wasCalled);
    }

    [Fact]
    public async Task ScaffoldIntoAsync_CreatesFilesAndDirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_tmpl_{Guid.NewGuid():N}");
        try
        {
            var vm = new TemplateGalleryViewModel();
            vm.SelectedTemplate = vm.Templates[0];
            vm.ModName = "MyTestMod";
            vm.ModDescription = "A test mod";
            vm.ModAuthor = "Tester";

            var (files, error) = await vm.ScaffoldIntoAsync(tempDir);

            Assert.Null(error);
            Assert.NotNull(files);
            Assert.True(files.Length > 0);
            Assert.True(Directory.Exists(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScaffoldIntoAsync_NoTemplate_ReturnsError()
    {
        var vm = new TemplateGalleryViewModel();
        var (files, error) = await vm.ScaffoldIntoAsync(Path.GetTempPath());
        Assert.Null(files);
        Assert.NotNull(error);
        Assert.Contains("No template", error);
    }

    [Fact]
    public async Task ScaffoldIntoAsync_EmptyModName_UsesDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_tmpl_def_{Guid.NewGuid():N}");
        try
        {
            var vm = new TemplateGalleryViewModel();
            vm.SelectedTemplate = vm.Templates[0];
            vm.ModName = "";

            var (files, error) = await vm.ScaffoldIntoAsync(tempDir);

            Assert.Null(error);
            Assert.NotNull(files);
            // Should use "MyMod" as default name
            var mainLua = Path.Combine(tempDir, "main.lua");
            Assert.True(File.Exists(mainLua));
            var content = await File.ReadAllTextAsync(mainLua);
            Assert.Contains("MyMod", content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScaffoldIntoAsync_SubstitutesPlaceholders()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_tmpl_sub_{Guid.NewGuid():N}");
        try
        {
            var vm = new TemplateGalleryViewModel();
            vm.SelectedTemplate = vm.Templates[0];
            vm.ModName = "CustomMod";
            vm.ModDescription = "My description";
            vm.ModAuthor = "MyAuthor";

            var (files, error) = await vm.ScaffoldIntoAsync(tempDir);

            Assert.Null(error);
            var mainLua = Path.Combine(tempDir, "main.lua");
            Assert.True(File.Exists(mainLua));
            var content = await File.ReadAllTextAsync(mainLua);
            Assert.Contains("CustomMod", content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScaffoldIntoAsync_EscapesLuaStringInModName()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_tmpl_esc_{Guid.NewGuid():N}");
        try
        {
            var vm = new TemplateGalleryViewModel();
            vm.SelectedTemplate = vm.Templates[0];
            vm.ModName = "Test\"Mod\\Name";

            var (files, error) = await vm.ScaffoldIntoAsync(tempDir);

            Assert.Null(error);
            var mainLua = Path.Combine(tempDir, "main.lua");
            Assert.True(File.Exists(mainLua));
            var content = await File.ReadAllTextAsync(mainLua);
            // Escaped quotes and backslashes
            Assert.Contains("Test\\\"Mod\\\\Name", content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SelectedTemplate_SetAndGet_WorksCorrectly()
    {
        var vm = new TemplateGalleryViewModel();
        var template = vm.Templates[0];
        vm.SelectedTemplate = template;
        Assert.Same(template, vm.SelectedTemplate);
    }

    [Fact]
    public void StatusMessage_SetAndGet_WorksCorrectly()
    {
        var vm = new TemplateGalleryViewModel();
        vm.StatusMessage = "Test status";
        Assert.Equal("Test status", vm.StatusMessage);
    }
}

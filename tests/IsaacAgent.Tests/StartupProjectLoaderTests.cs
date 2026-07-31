using Avalonia.Headless.XUnit;
using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// Startup --project apply seam: delegates to existing LoadProjectAsync (issue #79).
/// </summary>
[Collection("Avalonia")]
public class StartupProjectLoaderTests
{
    private static ProjectViewModel CreateProjectViewModel()
    {
        var logger = Mock.Of<ILogger<ProjectViewModel>>();
        return new ProjectViewModel(logger, new AppConfiguration(), new ScaffoldingService());
    }

    [AvaloniaFact]
    public async Task TryLoadAsync_ExistingDirectory_LoadsViaProjectViewModel()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_startup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "main.lua"), "-- fixture");
            var project = CreateProjectViewModel();
            string? loaded = null;
            project.ProjectLoaded += path =>
            {
                loaded = path;
                return Task.CompletedTask;
            };

            await StartupProjectLoader.TryLoadAsync(tempDir, project);

            Assert.True(project.HasProject);
            Assert.Equal(tempDir, project.ProjectPath);
            Assert.Equal(tempDir, loaded);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [AvaloniaFact]
    public async Task TryLoadAsync_NullOrWhitespace_DoesNotLoad()
    {
        var project = CreateProjectViewModel();
        await StartupProjectLoader.TryLoadAsync(null, project);
        Assert.False(project.HasProject);

        await StartupProjectLoader.TryLoadAsync("   ", project);
        Assert.False(project.HasProject);
    }

    [AvaloniaFact]
    public async Task TryLoadAsync_MissingDirectory_DoesNotLoad()
    {
        var project = CreateProjectViewModel();
        var missing = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}");

        await StartupProjectLoader.TryLoadAsync(missing, project);

        Assert.False(project.HasProject);
        Assert.Equal("", project.ProjectPath);
    }
}

using IsaacAgent.App.ViewModels;

namespace IsaacAgent.App.Services;

/// <summary>
/// Applies an optional launch-time project path via existing
/// <see cref="ProjectViewModel.LoadProjectAsync"/> (no DI bypass).
/// </summary>
internal static class StartupProjectLoader
{
    /// <summary>
    /// Loads <paramref name="projectPath"/> when non-blank. Missing directories
    /// are a no-op inside <see cref="ProjectViewModel.LoadProjectAsync"/>.
    /// </summary>
    public static Task TryLoadAsync(string? projectPath, ProjectViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (string.IsNullOrWhiteSpace(projectPath))
            return Task.CompletedTask;

        return project.LoadProjectAsync(projectPath);
    }
}

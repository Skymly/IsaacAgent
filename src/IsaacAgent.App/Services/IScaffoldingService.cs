using IsaacAgent.Core.Models;

namespace IsaacAgent.App.Services;

/// <summary>
/// App façade for creating mod skeletons and gallery templates under a project directory.
/// ViewModels use this seam; the Agent <c>ToolRegistry</c> still constructs
/// <c>ScaffoldModTool</c> for the LLM tool loop.
/// </summary>
public interface IScaffoldingService
{
    /// <summary>
    /// Creates a basic mod skeleton (main.lua, metadata.xml, resources/) under
    /// <paramref name="projectDir"/>.
    /// </summary>
    Task CreateSkeletonAsync(
        string projectDir,
        string name,
        string? description = null,
        string? author = null,
        CancellationToken ct = default);

    /// <summary>
    /// Scaffolds a gallery <paramref name="template"/> into <paramref name="targetDir"/>
    /// with placeholder substitution. Returns created relative paths, or an error.
    /// </summary>
    Task<(string[]? Files, string? Error)> ScaffoldFromTemplateAsync(
        string targetDir,
        ModTemplate template,
        string? name = null,
        string? description = null,
        string? author = null,
        CancellationToken ct = default);
}

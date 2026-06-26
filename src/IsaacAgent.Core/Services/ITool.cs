using IsaacAgent.Core.Models;

namespace IsaacAgent.Core.Services;

/// <summary>
/// Defines an agent tool that can be invoked by the LLM.
/// </summary>
public interface ITool
{
    /// <summary>Unique name of the tool.</summary>
    string Name { get; }

    /// <summary>Human-readable description shown to the LLM.</summary>
    string Description { get; }

    /// <summary>JSON schema definition for the tool's parameters.</summary>
    ToolDefinition Definition { get; }

    /// <summary>
    /// Executes the tool with the given JSON arguments and returns a string result.
    /// </summary>
    Task<string> ExecuteAsync(string arguments, CancellationToken ct = default);
}

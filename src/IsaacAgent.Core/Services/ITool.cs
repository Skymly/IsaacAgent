using IsaacAgent.Core.Models;

namespace IsaacAgent.Core.Services;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolDefinition Definition { get; }
    Task<string> ExecuteAsync(string arguments, CancellationToken ct = default);
}

public sealed class ToolResult
{
    public required bool Success { get; init; }
    public required string Output { get; init; }
    public string? Error { get; init; }
}

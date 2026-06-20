using System.Collections.Concurrent;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Agent.Engine;

public sealed class ToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new();
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(ILogger<ToolRegistry> logger) => _logger = logger;

    public void Register(ITool tool)
    {
        if (!_tools.TryAdd(tool.Name, tool))
            throw new InvalidOperationException($"Tool '{tool.Name}' is already registered.");
    }

    public void RegisterAll(IEnumerable<ITool> tools)
    {
        foreach (var tool in tools)
            Register(tool);
    }

    public ITool? Get(string name) => _tools.TryGetValue(name, out var tool) ? tool : null;

    public List<ToolDefinition> GetDefinitions() => _tools.Values.Select(t => t.Definition).ToList();

    public async Task<string> ExecuteAsync(string toolName, string arguments, CancellationToken ct = default)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            _logger.LogWarning("Tool not found: {ToolName}", toolName);
            return $"Error: Tool '{toolName}' not found.";
        }

        try
        {
            _logger.LogInformation("Executing tool: {ToolName} with args: {Args}", toolName, arguments);
            var result = await tool.ExecuteAsync(arguments, ct);
            _logger.LogInformation("Tool {ToolName} completed", toolName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {ToolName} failed", toolName);
            return $"Error executing {toolName}: {ex.Message}";
        }
    }
}

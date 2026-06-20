using System.Text.Json;
using IsaacAgent.Core.Knowledge;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Tools.Implementations;

public sealed class SearchApiTool : ITool
{
    public string Name => "search_isaac_api";
    public string Description => "Search the Binding of Isaac: Repentance modding API documentation. Use this to look up classes, methods, callbacks, enums, and constants.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["query"] = new() { Type = "string", Description = "The search query (class name, method, callback, enum, etc.)" },
                ["category"] = new() { Type = "string", Description = "Optional category filter: 'class', 'callback', 'enum'", Enum = ["class", "callback", "enum"] }
            },
            Required = ["query"]
        }
    };

    public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var query = args.GetProperty("query").GetString()!;
        var category = args.TryGetProperty("category", out var c) ? c.GetString() : null;

        var results = new List<string>();

        if (category is null or "callback")
        {
            foreach (var (name, info) in ModCallbacks.Callbacks)
            {
                if (name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    info.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add($"[Callback] {name} (ID: {info.Id})\n  Args: {info.Args}\n  OptionalArgs: {info.OptionalArgs}\n  Description: {info.Description}");
                }
            }
        }

        if (category is null or "class")
        {
            foreach (var (name, info) in IsaacClasses.Classes)
            {
                if (name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    info.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    var methods = info.Methods.Where(m => m.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (methods.Count > 0)
                    {
                        results.Add($"[Class] {name} ({info.Category})\n  {info.Description}\n  Matching methods:\n" + string.Join('\n', methods.Select(m => "    " + m)));
                    }
                    else if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add($"[Class] {name} ({info.Category})\n  {info.Description}\n  Methods ({info.Methods.Length}):\n" + string.Join('\n', info.Methods.Take(10).Select(m => "    " + m)));
                    }
                }
            }
        }

        if (category is null or "enum")
        {
            foreach (var (name, info) in IsaacEnums.Enums)
            {
                if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add($"[Enum] {name}\n  {info.Description}\n  Values:\n" + string.Join('\n', info.Values.Take(20).Select(v => "    " + v)));
                }
                else
                {
                    var matchingValues = info.Values.Where(v => v.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matchingValues.Count > 0)
                    {
                        results.Add($"[Enum] {name} (matching values)\n  {info.Description}\n  Matching:\n" + string.Join('\n', matchingValues.Select(v => "    " + v)));
                    }
                }
            }
        }

        return Task.FromResult(results.Count > 0
            ? string.Join("\n\n", results)
            : $"No results found for '{query}'.");
    }
}

public sealed class GetCallbackInfoTool : ITool
{
    public string Name => "get_callback_info";
    public string Description => "Get detailed information about a specific Binding of Isaac mod callback, including its ID, arguments, and usage.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["name"] = new() { Type = "string", Description = "The callback name (e.g., MC_POST_UPDATE, MC_USE_ITEM)" }
            },
            Required = ["name"]
        }
    };

    public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var name = args.GetProperty("name").GetString()!;

        if (ModCallbacks.Callbacks.TryGetValue(name, out var info))
        {
            return Task.FromResult($"Callback: {name}\nID: {info.Id}\nArguments: {info.Args}\nOptionalArgs: {info.OptionalArgs}\nDescription: {info.Description}\n\nExample:\n```lua\nlocal mod = RegisterMod(\"MyMod\", 1)\nmod:AddCallback(ModCallbacks.{name}, function(_)\n    -- Your code here\nend)\n```");
        }

        var suggestions = ModCallbacks.Callbacks.Keys
            .Where(k => k.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult(suggestions.Count > 0
            ? $"Callback '{name}' not found. Did you mean: {string.Join(", ", suggestions)}?"
            : $"Callback '{name}' not found.");
    }
}

public sealed class GetClassInfoTool : ITool
{
    public string Name => "get_class_info";
    public string Description => "Get detailed information about a specific Binding of Isaac API class, including all its methods.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["name"] = new() { Type = "string", Description = "The class name (e.g., EntityPlayer, Room, Game)" }
            },
            Required = ["name"]
        }
    };

    public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var name = args.GetProperty("name").GetString()!;

        if (IsaacClasses.Classes.TryGetValue(name, out var info))
        {
            return Task.FromResult($"Class: {name}\nCategory: {info.Category}\nDescription: {info.Description}\n\nMethods:\n" + string.Join('\n', info.Methods.Select(m => $"  - {m}")));
        }

        var suggestions = IsaacClasses.Classes.Keys
            .Where(k => k.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult(suggestions.Count > 0
            ? $"Class '{name}' not found. Did you mean: {string.Join(", ", suggestions)}?"
            : $"Class '{name}' not found.");
    }
}

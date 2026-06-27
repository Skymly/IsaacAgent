using System.Text.Json;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Rag.Tools;

public sealed class SearchKnowledgeTool : ITool
{
    private readonly IRetriever _retriever;

    /// <summary>Optional callback to report retrieval results for UI visualization.</summary>
    public Action<string, IReadOnlyList<RetrievalResult>>? OnRetrievalResults { get; set; }

    public SearchKnowledgeTool(IRetriever retriever) => _retriever = retriever;

    public string Name => "search_knowledge";
    public string Description => "Search the Isaac modding knowledge base (API docs, examples, patterns) using semantic search. Use this for 'how do I...' questions, finding patterns, or looking up API usage examples.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["query"] = new() { Type = "string", Description = "Natural language query about Isaac modding" },
                ["top_k"] = new() { Type = "integer", Description = "Number of results to return (default 5, max 10)" },
                ["category"] = new() { Type = "string", Description = "Optional filter: 'callback', 'class', 'enum', 'example'", Enum = ["callback", "class", "enum", "example"] }
            },
            Required = ["query"]
        }
    };

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var query = args.GetProperty("query").GetString()!;
        var topK = args.TryGetProperty("top_k", out var tk) ? tk.GetInt32() : 5;
        topK = Math.Clamp(topK, 1, 10);
        var category = args.TryGetProperty("category", out var c) ? c.GetString() : null;

        var results = await _retriever.SearchAsync(query, topK, category, ct);

        OnRetrievalResults?.Invoke(query, results);

        if (results.Count == 0)
            return $"No knowledge base results for '{query}'. The index may not be built yet.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {results.Count} results for '{query}':\n");
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            sb.AppendLine($"### Result {i + 1} (score: {r.Score:F3}) — [{r.Chunk.Category}] {r.Chunk.Title}");
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

public sealed class GetPatternTool : ITool
{
    private readonly IRetriever _retriever;

    /// <summary>Optional callback to report retrieval results for UI visualization.</summary>
    public Action<string, IReadOnlyList<RetrievalResult>>? OnRetrievalResults { get; set; }

    public GetPatternTool(IRetriever retriever) => _retriever = retriever;

    public string Name => "get_pattern";
    public string Description => "Find code patterns and examples for common Isaac modding tasks. Available patterns include: custom collectible (passive/active), custom familiar, custom boss, custom room, custom challenge, custom character, custom trinket, custom card/pill, custom curse, custom door, custom pedestal, custom shop, custom devil room, custom tear effect, custom familiars advanced, custom hud, custom music, custom cutscene, custom status effect, save data, achievement tracking, item pool modification, multiplayer sync, and REPENTOGON ImGui menu. Use this when the user asks to create something new or wants a code example.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["task"] = new() { Type = "string", Description = "The modding task or pattern to find examples for" },
                ["top_k"] = new() { Type = "integer", Description = "Number of examples to return (default 3)" }
            },
            Required = ["task"]
        }
    };

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var task = args.GetProperty("task").GetString()!;
        var topK = args.TryGetProperty("top_k", out var tk) ? tk.GetInt32() : 3;
        topK = Math.Clamp(topK, 1, 5);

        var results = await _retriever.SearchAsync(task, topK, "example", ct);

        OnRetrievalResults?.Invoke(task, results);

        if (results.Count == 0)
            return $"No example patterns found for '{task}'. Try using search_knowledge for API documentation.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Patterns for '{task}':\n");
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            sb.AppendLine($"### Example {i + 1} (relevance: {r.Score:F3}) — {r.Chunk.Title}");
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

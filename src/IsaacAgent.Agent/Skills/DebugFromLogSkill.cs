using System.Text;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Agent.Skills;

/// <summary>
/// Guides the agent through debugging runtime errors from the Isaac log.
/// Pre-fetches the save data pattern (common source of errors) and injects
/// a diagnostic workflow.
/// </summary>
public sealed class DebugFromLogSkill : ISkill
{
    public string Name => "debug-from-log";
    public string DisplayName => "Debug from Log";
    public string Description => "Debug a runtime error by parsing log.txt, diagnosing Lua, and proposing a fix";
    public string? SlashCommand => "/debug";

    private static readonly string[] Keywords =
        ["crash", "error", "bug", "broken", "not working", "doesn't work", "debug", "fix", "wrong"];

    public bool ShouldActivate(string userMessage, string? projectDir)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.StartsWith("/")) return false;
        return Keywords.Any(k => lower.Contains(k));
    }

    public string? GetPromptAugmentation()
    {
        return """
            ## Active Skill: Debug From Log
            Follow this diagnostic workflow:
            1. Call parse_log to extract Lua errors from log.txt
            2. For each error found, identify the file and line number
            3. Call read_file on each affected file
            4. Call diagnose_lua on the affected file for static analysis
            5. If an API usage looks suspicious, call search_isaac_api or get_callback_info to verify
            6. Identify the root cause and explain it to the user
            7. Propose a fix and apply it with diff_apply (preferred) or write_file
            8. Call diagnose_lua again to confirm the fix resolves the issue
            9. Summarize: what was wrong, what was changed, and why
            """;
    }

    public async Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
        string userMessage, IRetriever? retriever, CancellationToken ct = default)
    {
        if (retriever is null || !retriever.IsReady) return [];

        // Pre-fetch common debugging knowledge
        var results = await retriever.SearchAsync("debugging Lua errors callbacks common issues", topK: 3, ct: ct);
        if (results.Count == 0) return [];

        var sb = new StringBuilder();
        sb.AppendLine("## Pre-fetched: Common Isaac Debugging Knowledge");
        foreach (var r in results)
        {
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }
        return [ChatMessage.System(sb.ToString())];
    }
}

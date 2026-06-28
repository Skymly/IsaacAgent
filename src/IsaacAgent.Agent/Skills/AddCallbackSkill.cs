using System.Text;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Agent.Skills;

/// <summary>
/// Guides the agent through adding a callback to an existing main.lua.
/// Pre-fetches callback info from RAG when a callback name is detected.
/// </summary>
public sealed class AddCallbackSkill : ISkill
{
    public string Name => "add-callback";
    public string Description => "Add a specific Isaac callback to main.lua with proper signature and parameters";
    public string? SlashCommand => "/add-callback";

    private static readonly string[] Keywords =
        ["add callback", "add a callback", "register callback", "use callback", "hook into", "listen to"];

    public bool ShouldActivate(string userMessage, string? projectDir)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.StartsWith("/")) return false;
        return Keywords.Any(k => lower.Contains(k));
    }

    public string? GetPromptAugmentation()
    {
        return """
            ## Active Skill: Add Callback
            Follow this workflow precisely:
            1. Identify which callback the user wants (e.g., MC_POST_PEFFECT_UPDATE,
               MC_ENTITY_TAKE_DMG, MC_POST_NEW_ROOM, etc.)
            2. Call get_callback_info with the callback name to get its:
               - Callback ID (ModCallbacks.MC_*)
               - Parameters and their types
               - Return value semantics (if any)
            3. Call read_file on main.lua to understand the current structure
            4. Determine the correct insertion point (after existing callbacks,
               before the last line)
            5. Write the callback with proper syntax:
               ```lua
               mod:AddCallback(ModCallbacks.MC_CALLBACK_NAME, function(_, param1, param2)
                   -- Implementation
               end, optionalFilter)
               ```
            6. Use diff_apply to insert the callback (preferred over write_file
               for targeted insertion into existing files)
            7. Call diagnose_lua to verify the callback is correctly registered
            8. Explain to the user what the callback does and when it fires
            """;
    }

    public async Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
        string userMessage, IRetriever? retriever, CancellationToken ct = default)
    {
        if (retriever is null || !retriever.IsReady) return [];

        // Try to extract a callback name from the message
        var match = System.Text.RegularExpressions.Regex.Match(userMessage, @"(MC_\w+)");
        if (!match.Success) return [];

        var callbackName = match.Groups[1].Value;
        var results = await retriever.SearchAsync($"callback {callbackName} usage parameters", topK: 2, ct: ct);
        if (results.Count == 0) return [];

        var sb = new StringBuilder();
        sb.AppendLine($"## Pre-fetched: {callbackName}");
        foreach (var r in results)
        {
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }
        return [ChatMessage.System(sb.ToString())];
    }
}

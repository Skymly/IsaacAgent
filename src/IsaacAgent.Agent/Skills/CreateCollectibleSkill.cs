using System.Text;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Agent.Skills;

/// <summary>
/// Guides the agent through creating a custom collectible (passive or active item).
/// Pre-fetches the relevant code pattern from RAG and injects a step-by-step workflow.
/// </summary>
public sealed class CreateCollectibleSkill : ISkill
{
    public string Name => "create-collectible";
    public string DisplayName => "Create Collectible";
    public string Description => "Create a custom collectible (passive/active item) with proper callbacks, items.xml, and validation";
    public string? SlashCommand => "/create-item";

    private static readonly string[] Keywords =
        ["create", "add", "make", "collectible", "item", "passive item", "active item", "trinket"];

    public bool ShouldActivate(string userMessage, string? projectDir)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.StartsWith("/")) return false; // explicit slash commands handled elsewhere
        var hasCreateVerb = lower.Contains("create") || lower.Contains("add") || lower.Contains("make");
        var hasItemNoun = lower.Contains("collectible") || lower.Contains("passive item")
            || lower.Contains("active item") || (lower.Contains("item") && !lower.Contains("item pool"));
        return hasCreateVerb && hasItemNoun;
    }

    public string? GetPromptAugmentation()
    {
        return """
            ## Active Skill: Create Custom Collectible
            Follow this workflow precisely:
            1. Determine whether the user wants a passive or active item
            2. Call get_pattern with "custom collectible passive" or "custom collectible active"
            3. If no project exists, call scaffold_mod with includeItems=true
            4. Read existing items.xml (if any) to find the next available ID
            5. Write main.lua with the appropriate callbacks:
               - Passive: MC_EVALUATE_CACHE for stat mods, MC_POST_ADD_COLLECTIBLE for pickup effects
               - Active: MC_USE_ITEM for the active effect
            6. Write/update items.xml with the new item entry (use ID >= 710100 for custom items)
            7. Call validate_xml on items.xml
            8. Call diagnose_lua on main.lua
            9. Report the item name, ID, and type to the user
            """;
    }

    public async Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
        string userMessage, IRetriever? retriever, CancellationToken ct = default)
    {
        if (retriever is null || !retriever.IsReady) return [];

        var isActive = userMessage.Contains("active", StringComparison.OrdinalIgnoreCase);
        var patternQuery = isActive ? "custom collectible active item" : "custom collectible passive item";

        var results = await retriever.SearchAsync(patternQuery, topK: 3, categoryFilter: "pattern", ct);
        if (results.Count == 0) return [];

        var sb = new StringBuilder();
        sb.AppendLine("## Pre-fetched Pattern: Custom Collectible");
        foreach (var r in results)
        {
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }
        return [ChatMessage.System(sb.ToString())];
    }
}

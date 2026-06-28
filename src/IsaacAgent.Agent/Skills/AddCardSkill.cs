using System.Text;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Agent.Skills;

/// <summary>
/// Guides the agent through adding a custom card or rune to an existing mod.
/// Pre-fetches the card pattern from RAG.
/// </summary>
public sealed class AddCardSkill : ISkill
{
    public string Name => "add-card";
    public string DisplayName => "Add Card / Rune";
    public string Description => "Add a custom card or rune to an existing mod with use effect and pickup metadata";
    public string? SlashCommand => "/add-card";

    private static readonly string[] Keywords =
        ["card", "rune", "tarot card", "suit card", "playing card"];

    public bool ShouldActivate(string userMessage, string? projectDir)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.StartsWith("/")) return false;
        var hasAddVerb = lower.Contains("add") || lower.Contains("create") || lower.Contains("make");
        var hasCardNoun = Keywords.Any(k => lower.Contains(k));
        return hasAddVerb && hasCardNoun;
    }

    public string? GetPromptAugmentation()
    {
        return """
            ## Active Skill: Add Custom Card / Rune
            Follow this workflow precisely:
            1. Call get_pattern with "custom card" to retrieve the card pattern
            2. Call read_file on main.lua to understand the current mod structure
            3. Call read_file on items.xml (or metadata.xml) to find the next
               available card ID (custom cards start at 32768+)
            4. Determine if this is a card or a rune:
               - Card: single-use, drawn from card pool
               - Rune: single-use, drawn from rune pool (requires CardType.CARD_RUNE)
            5. Add the card to items.xml:
               ```xml
               <card id="N" name="Card Name"
                     description="Effect description"
                     gfx="gfx/ui/cards/yourcard.png"
                     cache="all" />
               ```
            6. Add MC_USE_CARD callback for the card's use effect:
               ```lua
               mod:AddCallback(ModCallbacks.MC_USE_CARD, function(_, card, player, useFlags)
                   if card ~= YourCardId then return end
                   -- Apply effect
                   return true
               end)
               ```
            7. If the card should be pool-restricted, add MC_POST_GET_CARD
            8. Use diff_apply to insert the callbacks into main.lua
            9. Call validate_xml on items.xml
            10. Call diagnose_lua on main.lua
            11. Report the card name, ID, and effect to the user
            """;
    }

    public async Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
        string userMessage, IRetriever? retriever, CancellationToken ct = default)
    {
        if (retriever is null || !retriever.IsReady) return [];

        var isRune = userMessage.Contains("rune", StringComparison.OrdinalIgnoreCase);
        var query = isRune ? "custom card rune CardType CARD_RUNE" : "custom card tarot use effect";

        var results = await retriever.SearchAsync(query, topK: 3, categoryFilter: "pattern", ct: ct);
        if (results.Count == 0) return [];

        var sb = new StringBuilder();
        sb.AppendLine("## Pre-fetched Pattern: Custom Card / Rune");
        foreach (var r in results)
        {
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }
        return [ChatMessage.System(sb.ToString())];
    }
}

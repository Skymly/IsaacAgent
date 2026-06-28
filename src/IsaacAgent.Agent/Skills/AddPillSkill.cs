using System.Text;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Agent.Skills;

/// <summary>
/// Guides the agent through adding a custom pill effect to an existing mod.
/// Pre-fetches the pill pattern from RAG.
/// </summary>
public sealed class AddPillSkill : ISkill
{
    public string Name => "add-pill";
    public string Description => "Add a custom pill effect to an existing mod with use effect and pill pool registration";
    public string? SlashCommand => "/add-pill";

    private static readonly string[] Keywords =
        ["pill", "pill effect", "horse pill"];

    public bool ShouldActivate(string userMessage, string? projectDir)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.StartsWith("/")) return false;
        var hasAddVerb = lower.Contains("add") || lower.Contains("create") || lower.Contains("make");
        var hasPillNoun = Keywords.Any(k => lower.Contains(k));
        return hasAddVerb && hasPillNoun;
    }

    public string? GetPromptAugmentation()
    {
        return """
            ## Active Skill: Add Custom Pill
            Follow this workflow precisely:
            1. Call get_pattern with "custom pill" to retrieve the pill pattern
            2. Call read_file on main.lua to understand the current mod structure
            3. Determine the pill effect ID (custom pill effects start at 2049+)
            4. Add the pill to pills.xml:
               ```xml
               <pill id="N" name="Pill Name"
                     description="Effect description"
                     gfx="gfx/ui/pills/yourpill.png" />
               ```
            5. Add MC_USE_PILL callback for the pill's use effect:
               ```lua
               mod:AddCallback(ModCallbacks.MC_USE_PILL, function(_, pillEffect, player, useFlags)
                   if pillEffect ~= YourPillId then return end
                   -- Apply effect
                   return true
               end)
               ```
            6. If the pill has a horse pill variant, check useFlags for
               UseFlag.USE_HORSE_PILL and apply the stronger effect
            7. Use diff_apply to insert the callbacks into main.lua
            8. Call validate_xml on pills.xml
            9. Call diagnose_lua on main.lua
            10. Report the pill name, ID, and effect to the user
            """;
    }

    public async Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
        string userMessage, IRetriever? retriever, CancellationToken ct = default)
    {
        if (retriever is null || !retriever.IsReady) return [];

        var isHorse = userMessage.Contains("horse", StringComparison.OrdinalIgnoreCase);
        var query = isHorse ? "custom pill horse pill USE_HORSE_PILL" : "custom pill effect MC_USE_PILL";

        var results = await retriever.SearchAsync(query, topK: 3, categoryFilter: "pattern", ct: ct);
        if (results.Count == 0) return [];

        var sb = new StringBuilder();
        sb.AppendLine("## Pre-fetched Pattern: Custom Pill");
        foreach (var r in results)
        {
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }
        return [ChatMessage.System(sb.ToString())];
    }
}

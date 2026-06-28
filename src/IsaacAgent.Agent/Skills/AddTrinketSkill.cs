using System.Text;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Agent.Skills;

/// <summary>
/// Guides the agent through adding a custom trinket to an existing mod.
/// Pre-fetches the trinket pattern from RAG.
/// </summary>
public sealed class AddTrinketSkill : ISkill
{
    public string Name => "add-trinket";
    public string DisplayName => "Add Trinket";
    public string Description => "Add a custom trinket to an existing mod with pocket-active effect and pickup metadata";
    public string? SlashCommand => "/add-trinket";

    private static readonly string[] Keywords =
        ["trinket", "pocket active", "swallowed penny", "broken magnet"];

    public bool ShouldActivate(string userMessage, string? projectDir)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.StartsWith("/")) return false;
        var hasAddVerb = lower.Contains("add") || lower.Contains("create") || lower.Contains("make");
        var hasTrinketNoun = Keywords.Any(k => lower.Contains(k));
        return hasAddVerb && hasTrinketNoun;
    }

    public string? GetPromptAugmentation()
    {
        return """
            ## Active Skill: Add Custom Trinket
            Follow this workflow precisely:
            1. Call get_pattern with "custom trinket" to retrieve the trinket pattern
            2. Call read_file on main.lua to understand the current mod structure
            3. Call read_file on items.xml (or metadata.xml) to find the next
               available trinket ID (custom trinkets start at 32768+)
            4. Add the trinket to items.xml:
               ```xml
               <trinket id="N" name="Trinket Name"
                        description="Effect description"
                        gfx="gfx/ui/trinket_yourname.png"
                        cache="all" />
               ```
            5. Add MC_POST_TRINKET_INIT callback to set initial state
            6. Add MC_POST_PEFFECT_UPDATE callback for the passive effect
            7. If the trinket has a pocket-active effect, add MC_USE_TRINKET
               (optional — most trinkets are passive)
            8. Use diff_apply to insert the callbacks into main.lua
            9. Call validate_xml on items.xml
            10. Call diagnose_lua on main.lua
            11. Report the trinket name, ID, and effect to the user
            """;
    }

    public async Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
        string userMessage, IRetriever? retriever, CancellationToken ct = default)
    {
        if (retriever is null || !retriever.IsReady) return [];

        var results = await retriever.SearchAsync("custom trinket pocket active pickup", topK: 3, categoryFilter: "pattern", ct: ct);
        if (results.Count == 0) return [];

        var sb = new StringBuilder();
        sb.AppendLine("## Pre-fetched Pattern: Custom Trinket");
        foreach (var r in results)
        {
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }
        return [ChatMessage.System(sb.ToString())];
    }
}

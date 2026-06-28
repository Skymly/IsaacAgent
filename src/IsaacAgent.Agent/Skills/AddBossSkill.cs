using System.Text;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Agent.Skills;

/// <summary>
/// Guides the agent through adding a custom boss to an existing mod.
/// Pre-fetches the boss pattern from RAG.
/// </summary>
public sealed class AddBossSkill : ISkill
{
    public string Name => "add-boss";
    public string DisplayName => "Add Boss";
    public string Description => "Add a custom boss to an existing mod with AI, attacks, boss room spawning, and boss portrait";
    public string? SlashCommand => "/add-boss";

    private static readonly string[] Keywords =
        ["boss", "custom boss", "boss fight", "boss entity"];

    public bool ShouldActivate(string userMessage, string? projectDir)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.StartsWith("/")) return false;
        var hasAddVerb = lower.Contains("add") || lower.Contains("create") || lower.Contains("make");
        var hasBossNoun = Keywords.Any(k => lower.Contains(k));
        return hasAddVerb && hasBossNoun;
    }

    public string? GetPromptAugmentation()
    {
        return """
            ## Active Skill: Add Custom Boss
            Follow this workflow precisely:
            1. Call get_pattern with "custom boss" to retrieve the boss pattern
            2. Call read_file on main.lua to understand the current mod structure
            3. Call read_file on entities2.xml to find the next available entity ID
               (custom bosses typically use EntityType.ENTITY_BOSS variants or
               a custom entity type in the 900+ range)
            4. Add the boss to entities2.xml:
               ```xml
               <entity id="N" name="Boss Name"
                       gfx="gfx/bosses/yourboss.png"
                       boss="1"
                       collisionRadius="20"
                       friction="1" />
               ```
            5. Implement boss AI in main.lua using MC_NPC_UPDATE:
               ```lua
               mod:AddCallback(ModCallbacks.MC_NPC_UPDATE, function(_, boss)
                   if boss.Variant ~= YourBossVariant then return end
                   -- State machine: idle, chase, attack, special
                   -- Use boss:GetSprite() for animation
                   -- Use boss:GetData() for state storage
               end)
               ```
            6. Implement attack patterns:
               - Use a state machine (GetData()["state"]) for phase transitions
               - Spawn projectiles with Isaac.Spawn(EntityType.ENTITY_PROJECTILE, ...)
               - Use boss:GetSprite():Play("attack1") for telegraphing
            7. Add MC_POST_NEW_ROOM callback to spawn the boss in boss rooms
               (optional — depends on whether the boss should replace vanilla bosses)
            8. Add a boss portrait for the boss bar (bossbar.xml if applicable)
            9. Use diff_apply to insert the callbacks into main.lua
            10. Call validate_xml on entities2.xml
            11. Call diagnose_lua on main.lua
            12. Report the boss name, entity ID, and attack patterns to the user
            """;
    }

    public async Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
        string userMessage, IRetriever? retriever, CancellationToken ct = default)
    {
        if (retriever is null || !retriever.IsReady) return [];

        var results = await retriever.SearchAsync("custom boss NPC AI attack pattern boss room", topK: 3, categoryFilter: "pattern", ct: ct);
        if (results.Count == 0) return [];

        var sb = new StringBuilder();
        sb.AppendLine("## Pre-fetched Pattern: Custom Boss");
        foreach (var r in results)
        {
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }
        return [ChatMessage.System(sb.ToString())];
    }
}

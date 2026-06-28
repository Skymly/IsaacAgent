using System.Text;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Agent.Skills;

/// <summary>
/// Guides the agent through creating a custom familiar (companion entity).
/// Pre-fetches the familiar pattern from RAG and injects a step-by-step workflow.
/// </summary>
public sealed class CreateFamiliarSkill : ISkill
{
    public string Name => "create-familiar";
    public string DisplayName => "Create Familiar";
    public string Description => "Create a custom familiar (companion) with orbit/follow/shoot behavior, entities2.xml, and callbacks";
    public string? SlashCommand => "/create-familiar";

    private static readonly string[] Keywords =
        ["familiar", "companion", "follower", "orbit", "pet", "sprite companion"];

    public bool ShouldActivate(string userMessage, string? projectDir)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.StartsWith("/")) return false;
        var hasCreateVerb = lower.Contains("create") || lower.Contains("add") || lower.Contains("make");
        var hasFamiliarNoun = Keywords.Any(k => lower.Contains(k));
        return hasCreateVerb && hasFamiliarNoun;
    }

    public string? GetPromptAugmentation()
    {
        return """
            ## Active Skill: Create Custom Familiar
            Follow this workflow precisely:
            1. Determine the familiar behavior type:
               - Orbit: circles around the player (use math.cos/sin for position)
               - Follow: trails behind the player (lerp toward player position)
               - Shoot: fires tears at enemies (target nearest enemy)
               - Buff: provides an aura effect to the player
            2. Call get_pattern with "custom familiar" for basic patterns or
               "custom familiars advanced" for orbit/shoot/buff behaviors
            3. If no project exists, call scaffold_mod with includeEntities=true
            4. Read existing entities2.xml (if any) to find the next available variant ID
            5. Write main.lua with MC_FAMILIAR_INIT and MC_FAMILIAR_UPDATE callbacks
            6. Write/update entities2.xml with the familiar entity definition
               - Use EntityType.ENTITY_FAMILIAR as the base type
               - Set a unique variant ID (>= 100 for custom familiars)
            7. Add a CACHE_FAMILIARS callback if the familiar should spawn with an item
            8. Call validate_xml on entities2.xml
            9. Call diagnose_lua on main.lua
            10. Report the familiar name, variant ID, and behavior type to the user
            """;
    }

    public async Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
        string userMessage, IRetriever? retriever, CancellationToken ct = default)
    {
        if (retriever is null || !retriever.IsReady) return [];

        var isAdvanced = userMessage.Contains("orbit", StringComparison.OrdinalIgnoreCase)
            || userMessage.Contains("shoot", StringComparison.OrdinalIgnoreCase)
            || userMessage.Contains("buff", StringComparison.OrdinalIgnoreCase);
        var patternQuery = isAdvanced ? "custom familiars advanced orbit shoot buff" : "custom familiar";

        var results = await retriever.SearchAsync(patternQuery, topK: 3, categoryFilter: "pattern", ct: ct);
        if (results.Count == 0) return [];

        var sb = new StringBuilder();
        sb.AppendLine("## Pre-fetched Pattern: Custom Familiar");
        foreach (var r in results)
        {
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }
        return [ChatMessage.System(sb.ToString())];
    }
}

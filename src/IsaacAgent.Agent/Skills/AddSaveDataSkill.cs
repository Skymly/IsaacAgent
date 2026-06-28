using System.Text;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Agent.Skills;

/// <summary>
/// Guides the agent through adding save data persistence to an existing mod.
/// Pre-fetches the save data pattern from RAG.
/// </summary>
public sealed class AddSaveDataSkill : ISkill
{
    public string Name => "add-save-data";
    public string Description => "Add persistent save data to an existing mod using Isaac.SaveModData/LoadModData";
    public string? SlashCommand => "/add-save-data";

    private static readonly string[] Keywords =
        ["save data", "persist", "persistence", "save file", "save mod data", "load mod data", "across runs", "between runs"];

    public bool ShouldActivate(string userMessage, string? projectDir)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.StartsWith("/")) return false;
        var hasSaveKeyword = Keywords.Any(k => lower.Contains(k));
        var hasAddVerb = lower.Contains("add") || lower.Contains("implement") || lower.Contains("enable");
        return hasSaveKeyword && hasAddVerb;
    }

    public string? GetPromptAugmentation()
    {
        return """
            ## Active Skill: Add Save Data
            Follow this workflow precisely:
            1. Call get_pattern with "save data" to retrieve the save data pattern
            2. Call read_file on main.lua to understand the current structure
            3. Define a default save data table at the top of main.lua:
               ```lua
               local DefaultSaveData = {
                   -- fields the user wants to persist
               }
               local SaveData = {}
               ```
            4. Add MC_POST_GAME_STARTED callback to load save data:
               ```lua
               mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
                   if isSave then
                       local data = Isaac.LoadModData(mod)
                       if data then
                           SaveData = json.decode(data)
                       end
                   else
                       SaveData = TableCopy(DefaultSaveData)
                   end
               end)
               ```
            5. Add MC_PRE_GAME_EXIT callback to save data:
               ```lua
               mod:AddCallback(ModCallbacks.MC_PRE_GAME_EXIT, function(_, shouldSave)
                   if shouldSave then
                       Isaac.SaveModData(mod, json.encode(SaveData))
                   end
               end)
               ```
            6. Include json library: require("json") at the top of main.lua
            7. Use diff_apply to insert the callbacks (preferred over write_file)
            8. Call diagnose_lua to verify the changes
            9. Explain to the user how to access and modify SaveData in their code
            """;
    }

    public async Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
        string userMessage, IRetriever? retriever, CancellationToken ct = default)
    {
        if (retriever is null || !retriever.IsReady) return [];

        var results = await retriever.SearchAsync("save data persistence SaveModData LoadModData", topK: 3, categoryFilter: "pattern", ct: ct);
        if (results.Count == 0) return [];

        var sb = new StringBuilder();
        sb.AppendLine("## Pre-fetched Pattern: Save Data");
        foreach (var r in results)
        {
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }
        return [ChatMessage.System(sb.ToString())];
    }
}

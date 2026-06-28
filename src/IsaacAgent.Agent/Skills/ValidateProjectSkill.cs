using System.Text;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Agent.Skills;

/// <summary>
/// Guides the agent through validating an entire project before in-game testing.
/// Runs XML validation and Lua diagnostics on all relevant files.
/// </summary>
public sealed class ValidateProjectSkill : ISkill
{
    public string Name => "validate-project";
    public string Description => "Validate the entire mod project: check all XML files against schemas and run Lua diagnostics";
    public string? SlashCommand => "/validate";

    private static readonly string[] Keywords =
        ["validate", "check", "verify", "lint", "health", "issues", "problems"];

    public bool ShouldActivate(string userMessage, string? projectDir)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.StartsWith("/")) return false;
        // Only auto-activate if the user explicitly asks to validate/check the whole project
        var hasValidateVerb = Keywords.Any(k => lower.Contains(k));
        var hasProjectScope = lower.Contains("project") || lower.Contains("mod") || lower.Contains("everything")
            || lower.Contains("all files") || lower.Contains("before testing");
        return hasValidateVerb && hasProjectScope;
    }

    public string? GetPromptAugmentation()
    {
        return """
            ## Active Skill: Validate Project
            Follow this validation workflow:
            1. Call list_files to see the full project structure
            2. For each XML file found (metadata.xml, items.xml, entities2.xml, trinkets.xml,
               challenges.xml, players.xml, costumes.xml, etc.):
               - Call validate_xml on the file
               - Record any schema violations
            3. For each Lua file found (main.lua, and any require'd modules):
               - Call diagnose_lua on the file
               - Record any issues found
            4. Check for cross-file consistency:
               - Item IDs in items.xml match references in main.lua
               - Entity variants in entities2.xml match spawn calls in main.lua
               - Callback names are valid (vanilla or REPENTOGON)
            5. Compile a consolidated report:
               - Group by severity: errors, warnings, suggestions
               - For each issue, include file, line (if available), and description
            6. Offer to fix issues automatically using diff_apply or batch_edit
            7. After fixes, re-run validation to confirm
            """;
    }

    public async Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
        string userMessage, IRetriever? retriever, CancellationToken ct = default)
    {
        // No RAG pre-fetch needed — validation uses project files directly
        await Task.CompletedTask;
        return [];
    }
}

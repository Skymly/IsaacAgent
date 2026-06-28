using IsaacAgent.Core.Models;

namespace IsaacAgent.Core.Services;

/// <summary>
/// A skill represents a higher-level workflow that augments the agent's
/// behavior for specific Isaac modding tasks. Skills sit between the system
/// prompt and atomic tools: they inject task-specific guidance and can
/// pre-fetch RAG context before the LLM processes the request.
/// </summary>
public interface ISkill
{
    /// <summary>Unique kebab-case identifier (e.g. "create-collectible").</summary>
    string Name { get; }

    /// <summary>Human-readable description shown in the command palette.</summary>
    string Description { get; }

    /// <summary>
    /// Slash command trigger (e.g. "/create-item"). Null if the skill
    /// only activates via auto-detection.
    /// </summary>
    string? SlashCommand { get; }

    /// <summary>
    /// Determines if this skill should auto-activate for the given user
    /// message. Called before the LLM processes the request.
    /// </summary>
    bool ShouldActivate(string userMessage, string? projectDir);

    /// <summary>
    /// Returns additional system prompt content to inject when this skill
    /// is active. Returns null/empty if no augmentation is needed.
    /// </summary>
    string? GetPromptAugmentation();

    /// <summary>
    /// Pre-fetches context from RAG or other sources before the LLM
    /// processes the request. Returns additional system messages to
    /// prepend to the conversation, or an empty list if no pre-fetch
    /// is needed.
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
        string userMessage,
        IRetriever? retriever,
        CancellationToken ct = default);
}

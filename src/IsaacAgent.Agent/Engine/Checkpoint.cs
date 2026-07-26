using IsaacAgent.Core.Models;

namespace IsaacAgent.Agent.Engine;

/// <summary>
/// In-session conversation anchor created before a user message is processed.
/// </summary>
public sealed class Checkpoint
{
    public Checkpoint(Guid id, ChatMessage userMessage)
    {
        Id = id;
        UserMessage = userMessage ?? throw new ArgumentNullException(nameof(userMessage));
    }

    public Guid Id { get; }

    /// <summary>
    /// The user <see cref="ChatMessage"/> this Checkpoint anchors. Identity
    /// (reference) is the conversation cursor — valid while the message
    /// remains in the session history.
    /// </summary>
    public ChatMessage UserMessage { get; }
}

using IsaacAgent.Core.Models;

namespace IsaacAgent.App.Services;

/// <summary>
/// Chat session store: project-scoped persistence for multi-tab Agent history envelopes.
/// </summary>
public interface IChatSessionStore
{
    /// <summary>
    /// Saves the project manifest. No-op when <paramref name="projectDir"/> is null or empty
    /// (no read, no write).
    /// </summary>
    Task SaveAsync(string? projectDir, ProjectSessionManifest manifest, CancellationToken ct = default);

    /// <summary>
    /// Loads the project manifest. No-op empty result when no project is open.
    /// Missing or corrupt files fail soft (empty manifest).
    /// </summary>
    Task<ProjectSessionManifest> LoadAsync(string? projectDir, CancellationToken ct = default);
}

/// <summary>
/// One project manifest: ordered tabs with stable GUIDs and Agent-shaped message envelopes.
/// Does not carry Checkpoints, Before-images, or tip hashes.
/// </summary>
public sealed class ProjectSessionManifest
{
    public int Version { get; set; } = 1;
    public string ProjectDir { get; set; } = "";
    public List<SessionTabRecord> Tabs { get; set; } = [];
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// One chat tab in a project manifest. Payload mirrors the Agent history envelope.
/// </summary>
public sealed class SessionTabRecord
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    /// <summary>Agent history envelope version (compatible with AgentSession Version: 1).</summary>
    public int HistoryVersion { get; set; } = 1;
    public List<ChatMessage> Messages { get; set; } = [];
}

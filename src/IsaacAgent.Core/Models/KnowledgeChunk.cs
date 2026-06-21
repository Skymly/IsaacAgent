namespace IsaacAgent.Core.Models;

public sealed class KnowledgeChunk
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Category { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}

public sealed class RetrievalResult
{
    public required KnowledgeChunk Chunk { get; init; }
    public required float Score { get; init; }
}

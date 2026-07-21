using IsaacAgent.LLM;
using IsaacAgent.Rag.Embedding;

namespace IsaacAgent.App.Services;

/// <summary>
/// In-memory snapshot of LLM and embedding settings the user just confirmed —
/// what Settings apply consumes (not a second disk load).
/// </summary>
public sealed record ProviderIntent(ProviderConfig Chat, EmbeddingConfig Embedding);

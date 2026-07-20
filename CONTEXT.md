# IsaacAgent

Desktop AI coding agent for Binding of Isaac: Repentance Lua modding — local tools, skills, and knowledge retrieval.

## Language

### Settings and providers

**Settings apply**:
The App-side act of taking the user's saved provider intent and making the running session match it (chat provider swap; optionally kicking off embedding apply). Chrome concerns (language, theme, font size) are not part of Settings apply.
_Avoid_: Reload providers, App.Reload, configuration hot-reload (when you mean this act)

**Embedding apply**:
The Rag-side act of switching embedding source (including dimension changes), invalidating the old knowledge index / vectors, and rebuilding the index. One operation from the App's point of view.
_Avoid_: EmbeddingProviderProxy.Replace, ResetReady + RebuildIndex (as the public story)

**Provider intent**:
The in-memory snapshot of LLM and embedding settings the user just confirmed — what Settings apply consumes. Not a second read from disk.
_Avoid_: Loaded config, AppConfiguration.Load result (when you mean the snapshot Settings already holds)

### Knowledge

**Knowledge index**:
The local vector index used for semantic retrieval over embedded Isaac / modding knowledge.
_Avoid_: Vector DB, RAG database

**Embedding source**:
Where text→vector comes from for the knowledge index: bundled ONNX (default) or Ollama.
_Avoid_: Embedding backend, embedder engine

### Agent work

**Skill**:
Prompt / RAG guidance layered above atomic tools; does not replace tool calling.
_Avoid_: Workflow macro, agent mode (when you mean Skill)

**Tool**:
An atomic, schema-described capability the agent may invoke (file ops, scaffold, search, …).
_Avoid_: Function, action (when you mean Tool)

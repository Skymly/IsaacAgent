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

**User knowledge**:
App-global Markdown under `%APPDATA%\IsaacAgent\knowledge` that enters the knowledge index only after an explicit rebuild; same retrieval pool as bundled product knowledge. Not project files; not Chat session store.
_Avoid_: Custom knowledge, examples folder (legacy path name), user RAG corpus

### Agent work

**Skill**:
Prompt / RAG guidance layered above atomic tools; does not replace tool calling.
_Avoid_: Workflow macro, agent mode (when you mean Skill)

**Tool**:
An atomic, schema-described capability the agent may invoke (file ops, scaffold, search, …).
_Avoid_: Function, action (when you mean Tool)

**Agent tool-chain integration**:
A verification that a scripted LLM drive causes production Tools (via the real registry) to run against a real project directory with observable disk or validation outcomes. Distinct from orchestration-loop tests that substitute fake tools.
_Avoid_: Agent E2E (when you mean this), FlaUI E2E, RAG console e2e-test, process-level smoke (when you mean in-process tool-chain integration)

**Orchestration-loop test**:
A verification of the AgentSession multi-iteration tool-call loop using a scripted LLM and substitute tools, without requiring production Tool implementations or disk side effects.
_Avoid_: Agent tool-chain integration, Agent E2E (when you mean FakeTool orchestration)

### Chat persistence

**Chat session store**:
The App module that owns project-scoped chat persistence: one manifest file per project under `sessions/` whose authoritative payload is each tab's Agent history envelope; UI bubbles are a projection (user + assistant only). Uses stable tab GUIDs; does not persist when no project is open; Checkpoints remain ephemeral and are not stored. Supersedes the legacy dual paths (`chat-history/`, `history/`); on first load with no `sessions/` file, migrates once from those legacy roots then writes only under `sessions/`. Loading a saved chat session means hydrating tabs and AgentSession from this store — not Checkpoint Restore.
_Avoid_: ChatHistoryService.SaveSession / RestoreSession (removed legacy dual path), session deserialization restore (when you mean Checkpoint Restore)

### Checkpoints

**Checkpoint**:
An in-session anchor created automatically before a user message is processed in a live chat tab / AgentSession; Restore targets it.
_Avoid_: Restore point, Snapshot, Undo point (when you mean this anchor)

**Restore**:
The user act of returning a live session to a Checkpoint: truncate that turn and later conversation, and revert tracked tool writes per the Checkpoint contract. Distinct from loading a project’s Chat session store after open or project switch.
_Avoid_: Rewind, Rollback, Undo, load saved chat session (when you mean this act)

**Before-image**:
The captured prior file state (or tombstone for a create) recorded lazily before a tracked tool first mutates a path after a Checkpoint.
_Avoid_: File snapshot, Backup, Prior content (when you mean this capture)

**Tracked write**:
A project-file mutation by write_file, diff_apply, batch_edit, or scaffold_mod that the Checkpoint contract obligates to be restorable via Before-images. run_command mutations are not Tracked writes.
_Avoid_: Tool write, File mutation, Side effect (when you mean this obligation)

**Hand-edit**:
On-disk divergence from the content tip left by the agent's last successful Tracked write to that path, including deletion of the file.
_Avoid_: Manual edit, user tweak, dirty buffer (when you mean this divergence)

**Hand-edit conflict mode**:
Configurable Restore policy when a Hand-edit (or an unreadable path that cannot be compared to the tip) is present: force (default) always applies Before-images; skip leaves those paths unchanged, lists them, and still completes the rest of Restore (conversation truncate + safe Before-image applies).
_Avoid_: Conflict strategy, overwrite policy, merge mode

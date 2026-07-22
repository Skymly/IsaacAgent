# Research: Checkpoint tracked writes & lazy Before-image hook points

**Ticket:** [Skymly/IsaacAgent#25](https://github.com/Skymly/IsaacAgent/issues/25)  
**Branch:** `research/checkpoint-tracked-write-hooks`  
**Scope:** Product/design contract inventory only — no Checkpoint feature implementation.

## Contract (given; not reopened)

| Obligation | Tools |
|------------|--------|
| **Tracked writes** (must be restorable via Before-image) | `write_file`, `diff_apply`, `batch_edit`, `scaffold_mod` |
| **Untracked** (side effects not guaranteed restorable) | `run_command` |
| Capture timing | Lazy: on **first** tool mutation of a path after a Checkpoint, **before** the write |
| Restore scope | Agent/tool tracked writes within a live `AgentSession` only |

---

## 1. Inventory: tools that can mutate project files

Primary catalog: `docs/design/Tools.md` (API 面 tables) and registration in `ToolRegistry.ReconfigureForProject`.

### Mutating tools (filesystem side effects under the project directory)

| Tool name | Class | File | Mutation mechanism | Contract class |
|-----------|-------|------|--------------------|----------------|
| `write_file` | `WriteFileTool` | `src/IsaacAgent.Tools/Implementations/FileTools.cs` | `Directory.CreateDirectory` + `File.WriteAllTextAsync` after `FileToolPathSafety.Resolve` | **Tracked** |
| `diff_apply` | `DiffApplyTool` | `src/IsaacAgent.Tools/Implementations/ProjectTools.cs` | Reads lines, applies patch in memory, `File.WriteAllLines` after `FileToolPathSafety.Resolve` | **Tracked** |
| `batch_edit` | `BatchEditTool` | `src/IsaacAgent.Tools/Implementations/ProjectTools.cs` | Per-edit: resolve path, `File.ReadAllText` → replace → `File.WriteAllText`; may touch many paths | **Tracked** |
| `scaffold_mod` | `ScaffoldModTool` | `src/IsaacAgent.Tools/Implementations/ScaffoldModTool.cs` | Multiple `File.WriteAllTextAsync` + `Directory.CreateDirectory` under `_projectDir` (fixed filenames) | **Tracked** |
| `run_command` | `RunCommandTool` | `src/IsaacAgent.Tools/Implementations/ProjectTools.cs` | Spawns `cmd` / `/bin/sh` with `WorkingDirectory = _projectDir`; arbitrary process I/O | **Untracked** |

### Non-mutating tools (for completeness; no Before-image obligation)

Registered in the same `ToolRegistry.ReconfigureForProject` path when a project dir is set, but they only read or analyze:

| Tool name | Class | File | Notes |
|-----------|-------|------|-------|
| `read_file` | `ReadFileTool` | `FileTools.cs` | Read-only |
| `list_files` | `ListFilesTool` | `FileTools.cs` | Enumerate only |
| `diagnose_lua` | `DiagnoseLuaTool` | `DiagnoseLuaTool.cs` | Read + analyze |
| `git_status` | `GitStatusTool` | `ProjectTools.cs` | Runs `git` read-only args (`status` / `diff` / `log`); not a project file writer |
| `validate_xml` | `ValidateXmlTool` | `src/IsaacAgent.Rag/Tools/ValidateXmlTool.cs` | Read + validate |
| `parse_log` | `ParseLogTool` | `src/IsaacAgent.Rag/Tools/ParseLogTool.cs` | Read log |
| Knowledge / API tools | `SearchApiTool`, `GetCallbackInfoTool`, `GetClassInfoTool`, `SearchKnowledgeTool`, `GetPatternTool` | Tools / Rag | No project file writes |

**Repo-wide confirmation:** a search for `File.Write*` / `Directory.Create*` under `src/` tool implementations hits only `WriteFileTool`, `DiffApplyTool`, `BatchEditTool`, `ScaffoldModTool`, plus `ToolRegistry.ReconfigureForProject` creating the project directory itself (not a user tool call).

Citations:

- Design catalog: `docs/design/Tools.md` § API 面 (`write_file` / `diff_apply` / `batch_edit` / `scaffold_mod` / `run_command`).
- Registration: `ToolRegistry.ReconfigureForProject` — `src/IsaacAgent.Agent/Engine/ToolRegistry.cs` (registers `WriteFileTool`, `ScaffoldModTool`, `DiffApplyTool`, `BatchEditTool`, `RunCommandTool` when `projectDir` is set).
- Write sites: `WriteFileTool.ExecuteAsync` (`FileTools.cs`); `DiffApplyTool.ExecuteAsync` / `BatchEditTool.ExecuteAsync` / `RunCommandTool.ExecuteAsync` (`ProjectTools.cs`); `ScaffoldModTool.ExecuteAsync` (`ScaffoldModTool.cs`).

---

## 2. Path arguments available before mutation (for lazy capture)

| Tool | How to know target path(s) **before** write | Safety / resolve |
|------|---------------------------------------------|------------------|
| `write_file` | JSON `path` (+ `content`) | `FileToolPathSafety.Resolve(_projectDir, relPath)` then write |
| `diff_apply` | JSON `path` (+ `patch`) | Same `Resolve`; requires file already exists |
| `batch_edit` | JSON `edits[]` each with `path` | Per-edit `Resolve`; skip unsafe / missing / not-found find |
| `scaffold_mod` | No path args — always writes fixed names under `_projectDir`: `main.lua`, `metadata.xml`, optional `items.xml` / `trinkets.xml` / `entities2.xml`, plus `resources/gfx/` and `resources/scripts/` dirs | **Does not** call `FileToolPathSafety`; uses `Path.Combine(_projectDir, …)` only |
| `run_command` | Opaque `command` string — paths not structured | N/A for tracked restore |

`FileToolPathSafety` (`FileTools.cs`, `internal static class FileToolPathSafety`): `Resolve`, `IsWithinProject`, `NormalizeRelativePath` — the shared sandbox helper for explicit path tools.

---

## 3. Shared execution seams (where a lazy Before-image hook should attach)

### Preferred single seam: `ToolRegistry.ExecuteAsync`

**Symbol:** `ToolRegistry.ExecuteAsync(string toolName, string arguments, CancellationToken ct)`  
**File:** `src/IsaacAgent.Agent/Engine/ToolRegistry.cs`

Flow today:

1. Take `_registryLock` briefly → lookup `ITool` by name → release lock.
2. `await tool.ExecuteAsync(arguments, ct)` (mutation happens inside the tool).

**Why this is the best attach point**

- Every LLM tool invocation that can mutate files goes through this method (no parallel bypass).
- `toolName` and raw JSON `arguments` are available **before** any tool body runs — enough to derive path sets for the four tracked tools without guessing inside each writer.
- One place to implement: “if tracked write tool → resolve path set → for each path not yet captured since Checkpoint → capture Before-image → then call `ExecuteAsync`.”
- Keeps capture out of `ITool` implementations and out of UI (`OnToolCall` is fire-and-forget for display).

Suggested conceptual insertion (design only):

```text
lookup tool
if tool is tracked-write family:
  paths = DerivePaths(toolName, arguments, CurrentProjectDir)
  foreach path in paths: MaybeCaptureBeforeImage(path)  // lazy, first touch after Checkpoint
return await tool.ExecuteAsync(arguments, ct)
```

`CurrentProjectDir` is already on `ToolRegistry` and matches the directory baked into tool instances at `ReconfigureForProject`.

### Secondary seam: `AgentSession` tool loop

**Symbol:** `AgentSession.SendMessageAsync` tool-execution loop  
**File:** `src/IsaacAgent.Agent/Engine/AgentSession.cs`

Relevant sequence (sequential by design):

1. `OnToolCall?.Invoke(toolCall.Name, toolCall.Arguments);`
2. `result = await _tools.ExecuteAsync(toolCall.Name, toolCall.Arguments, ct);`
3. `OnToolResult?.Invoke(...)`.

Comments in this loop explicitly name `write_file`, `diff_apply`, `batch_edit`, `run_command` as writers and justify **sequential** (not parallel) tool execution to avoid races.

A hook here would also see name + args before `_tools.ExecuteAsync`, but:

- Duplicates path-derivation if Registry is also wrapped later.
- Session already owns Checkpoint lifetime conceptually (“live `AgentSession`”), so wiring Checkpoint *state* on the session and *capture* in Registry (or a small collaborator injected into Registry) is cleaner than putting filesystem snapshot logic in the chat loop.

**Recommendation:** implement lazy Before-image at **`ToolRegistry.ExecuteAsync`** (or a narrow interceptor called only from there); keep Checkpoint bookkeeping on **`AgentSession`**. Do **not** rely on `OnToolCall` alone (subscribers cannot block or fail the write).

### Not a shared seam (avoid as primary)

| Location | Why insufficient alone |
|----------|------------------------|
| Per-tool `ExecuteAsync` bodies | Four+ copies; easy to miss future writers; scaffold path set is implicit |
| `FileToolPathSafety.Resolve` | Not called by `scaffold_mod` or `run_command`; only resolves, does not write |
| `ITool` interface (`src/IsaacAgent.Core/Services/ITool.cs`) | No pre-execute hook; only `ExecuteAsync` |

---

## 4. Gaps and risks

### `batch_edit` — multi-file, partial apply

- Iterates edits; writes immediately per successful edit (`File.WriteAllText`).
- Design Doc known limitation: “`batch_edit` 无事务回滚” (`docs/design/Tools.md` § 已知局限).
- Lazy Before-image must capture **each distinct path on first touch in the call** (and across the session since Checkpoint) **before** that path’s first write in the loop — ideally by pre-scanning `edits[].path` at Registry before invoking the tool, not after mid-loop failures.
- Same path appearing multiple times in one `batch_edit`: one Before-image (first mutation after Checkpoint) is correct per contract.

### `scaffold_mod` — multi-file + directories, no path safety helper

- Always may overwrite `main.lua` / `metadata.xml` (and optionals) without checking prior existence.
- Creates empty directory trees (`resources/gfx/`, `resources/scripts/`).
- Restore must define: file content restore + “file did not exist” delete; directory-only creations may need explicit policy (empty dirs vs ignore).
- Hook path derivation must hardcode the same fixed names the tool writes (or refactor later to share a path list — out of scope here).

### `write_file` — create vs overwrite

- Creates parent directories; may create a **new** file.
- Before-image for missing files should be an explicit “absent” sentinel so restore deletes the file rather than writing empty content.

### `diff_apply`

- Fails closed if file missing or patch mismatch; Before-image of existing content is still required if the apply path will run — capture after safety/existence checks would be more precise but those checks live **inside** the tool. Registry-level capture of `path` when the file exists (or always capturing absent/present) remains implementable without guessing the patch outcome.
- If capture happens at Registry and apply later fails, an unused Before-image for that path is harmless for restore correctness (still represents pre-first-mutation state if a later tracked write succeeds).

### `run_command` — untracked by contract

- Can mutate any project (or reachable) files via shell despite blacklist (`IsDangerousCommand`).
- **Must not** be treated as restorable; Checkpoint restore of tracked tools will not undo shell side effects.
- Product UX should assume divergence if the agent used `run_command` after a Checkpoint.

### Other non-tool writes (out of tracked-write contract)

- `ToolRegistry.ReconfigureForProject`: `Directory.CreateDirectory(projectDir)`.
- `AgentSession.SaveHistoryAsync`: writes chat history JSON (session persistence, not mod project tracked writes).

### Parallelism

- Session already serializes tool calls — good for deterministic Before-image order. Do not reintroduce parallel tool execution without revisiting this contract.

---

## 5. Answer summary

**Which concrete tools mutate project files today?**  
`write_file` (`WriteFileTool`), `diff_apply` (`DiffApplyTool`), `batch_edit` (`BatchEditTool`), `scaffold_mod` (`ScaffoldModTool`), and `run_command` (`RunCommandTool`). No other registered tools write project files.

**Tracked vs untracked:** first four are tracked; `run_command` is untracked.

**Where to attach a lazy Before-image hook without guessing?**  
At **`ToolRegistry.ExecuteAsync`**, immediately before `tool.ExecuteAsync`, using `toolName` + JSON `arguments` (+ `CurrentProjectDir` / fixed scaffold paths) to enumerate paths. Optionally coordinate Checkpoint lifecycle from **`AgentSession`**, which is the sole caller of that execute path during agent turns and already runs tools sequentially.

---

## 6. Citation index

| Claim | Source |
|-------|--------|
| Tool catalog & names | `docs/design/Tools.md` |
| Tool registration | `ToolRegistry.ReconfigureForProject` — `src/IsaacAgent.Agent/Engine/ToolRegistry.cs` |
| Shared execute entry | `ToolRegistry.ExecuteAsync` — same file |
| Session sequential tool loop + writer comment | `AgentSession.SendMessageAsync` — `src/IsaacAgent.Agent/Engine/AgentSession.cs` |
| `write_file` / path safety | `WriteFileTool`, `FileToolPathSafety` — `src/IsaacAgent.Tools/Implementations/FileTools.cs` |
| `diff_apply` / `batch_edit` / `run_command` | `src/IsaacAgent.Tools/Implementations/ProjectTools.cs` |
| `scaffold_mod` multi-write | `src/IsaacAgent.Tools/Implementations/ScaffoldModTool.cs` |
| `ITool` contract | `src/IsaacAgent.Core/Services/ITool.cs` |
| `batch_edit` no transaction | `docs/design/Tools.md` § 已知局限 |

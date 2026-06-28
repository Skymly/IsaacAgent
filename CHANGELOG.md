# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- `MC_HUD_UPDATE` (ID 1020) and `MC_HUD_POST_UPDATE` (ID 1021) moved from
  `RepentogonModifiedIds` to `RepentogonCallbacks`. They are REPENTOGON-only
  callbacks (not vanilla overrides) and were misclassified.

### Added
- Knowledge-base accuracy guard tests (`ModCallbacksAccuracyGuardTests`):
  parse the embedded `ModCallbacks.md` tables and assert that every C#
  callback ID matches the official documentation. Covers vanilla callbacks
  (bidirectional exact match, all 74 IDs) and REPENTOGON callbacks
  (one-directional: every C# entry must appear in the markdown). Prevents
  recurrence of KB-1, a systematic ID mapping error.

### Security
- `ScaffoldModTool` constructor now normalizes the project path with
  `Path.GetFullPath`, closing a path-traversal gap.
- `AppConfiguration.Save` no longer falls back to plaintext API key storage
  when DPAPI encryption fails — the key is discarded with a warning instead.
- `IsDangerousCommand` rewritten to split commands on shell operators
  (`&&`, `||`, `;`, `|`) before checking each sub-command, and now detects
  PowerShell (`Remove-Item -Recurse`, `Invoke-Expression`, `Start-Process`)
  and Windows (`del /f /s /q`, `rd /s /q`) destructive patterns.
- `FileTools` file enumeration now skips reparse points (junctions/symlinks)
  via `EnumerateFilesSafe` to prevent junction-based traversal.
- Git invocations in `ProjectTools` hardened with `GIT_TERMINAL_PROMPT=0`,
  `GIT_SSH_COMMAND=ssh -oBatchMode=yes`, and `-c core.hooksPath=/dev/null`
  to prevent credential prompts and malicious hook redirection.
- `AgentSession` now sanitizes tool results with boundary markers to
  prevent LLM injection via forged tool output.

### Added
- Skills system: higher-level workflows that augment the agent's behavior
  for specific Isaac modding tasks. Each skill injects task-specific prompt
  guidance and pre-fetches relevant RAG context before the LLM processes
  the request. Skills activate automatically via keyword detection or
  explicitly via slash commands.
  - `ISkill` interface and `SkillRegistry` (singleton, DI-registered).
  - 10 built-in skills: Create Collectible (`/create-item`), Create Familiar
    (`/create-familiar`), Debug from Log (`/debug`), Validate Project
    (`/validate`), Add Callback (`/add-callback`), Add Save Data
    (`/add-save-data`), Add Trinket (`/add-trinket`), Add Card / Rune
    (`/add-card`), Add Pill (`/add-pill`), Add Boss (`/add-boss`).
  - Slash commands are invocable from the chat box or the command palette.
- `ISkill.DisplayName` property: each skill owns its short command-palette
  title. The command palette now enumerates `SkillRegistry` instead of
  maintaining a parallel hardcoded list — new skills appear automatically.
- Four new agent tools for enhanced project interaction:
  - `git_status` — shows git status, recent commits, and uncommitted diff
  - `diff_apply` — applies unified diff patches to files (more precise than
    write_file for large files with small changes)
  - `batch_edit` — applies multiple find-and-replace edits across files in
    a single call, reducing round-trips
  - `run_command` — runs shell commands in the project directory with
    timeout (30s default, max 120s) and dangerous-command blocking
- 13 new tests covering all four tools (diff apply add/remove/modify,
  path traversal, batch edit, run command echo/block/timeout, git status).
- Cross-platform CI matrix: `ci-lib` (ubuntu-latest, macos-latest) for
  library tests, `ci-windows` for full solution build + format check.
- Release build job: publishes self-contained win-x64 single-file
  executable on `v*` tag pushes, creates a draft GitHub Release.
- Nuke `CiLib` and `UnitTestLib` targets for cross-platform library testing.
- Character-budget history trimming in `AgentSession` (120k chars ~30k
  tokens) alongside the existing message-count limit, preventing context
  overflow from large tool results.
- Stream idle timeout (90s) for `OpenAICompatibleProvider` and
  `OllamaProvider` — stalled SSE/NDJSON streams now throw
  `TimeoutException` instead of hanging indefinitely.
- RAG index prewarm failure now surfaces in the Settings UI.
- `RepentogonCallbacks` dictionary with 130+ REPENTOGON-exclusive
  callbacks (IDs 1000+) and `RepentogonModifiedIds` for 12 vanilla
  callback overrides.
- `ModCallbacks.Lookup()` and `GetRepentogonId()` helper methods for
  cross-dictionary queries.
- `InternalsVisibleTo` for IsaacAgent.Tests from IsaacAgent.LLM.
- Version properties (`VersionPrefix`, `AssemblyVersion`, `FileVersion`)
  in `Directory.Build.props`.
- E2E test project (`IsaacAgent.E2ETest`) added to the solution.
- Comprehensive agent tools table in README with module attribution
  (expanded to 16 tools across Tools and Rag modules).
- 14 new code patterns added to the RAG pattern library (11 → 25 total):
  item pool modification, custom curse, achievement tracking, custom HUD,
  custom tear effect, advanced familiars (orbit/shoot/buff), custom shop,
  custom devil room, custom door, custom pedestal, custom status effect,
  custom music, custom cutscene, multiplayer sync.
- 5 few-shot workflow examples added to the system prompt to guide
  multi-step tool orchestration: create collectible, debug runtime error,
  validate project, add save data, create familiar.
- 8 tool descriptions enhanced with usage guidance and available pattern
  names to improve LLM tool selection accuracy.
- `AssemblyName` set to `IsaacAgent` for the App project so the published
  executable is `IsaacAgent.exe` (was `IsaacAgent.App.exe`).
- Release workflow now uses `fail_on_unmatched_files: true` to catch
  missing asset errors in CI.
- MIT LICENSE file added; README updated with license section.
- CONTRIBUTING.md with development setup, code style, and PR workflow.
- GitHub issue templates (bug report, feature request) and PR template.
- README enhanced with CI badges, quick start guide, UI feature table,
  and tool/knowledge base statistics.
- Command palette (Ctrl+Shift+P) with fuzzy search and keyboard navigation.
- Mod template gallery (Ctrl+Shift+T) with 5 built-in templates.
- Live log monitor with real-time Isaac log.txt parsing and error highlighting.
- Visual diff viewer (Ctrl+Shift+D) with side-by-side git diff visualization.

### Fixed
- `OnnxEmbeddingProvider.session.Run` now serialized with a lock to fix
  thread-safety crashes under concurrent embedding calls.
- `ChatServiceProxy.Replace` now uses `Interlocked.Exchange` for atomic
  provider swap and disposes the old provider; implements `IDisposable`.
- `OpenAICompatibleProvider`, `OllamaProvider`, and `OllamaEmbeddingProvider`
  now implement `IDisposable` to properly dispose `HttpClient`.
- `EmbeddingProviderProxy.Replace` validates dimension match before swap.
- Streaming JSON parsing in both LLM providers now tolerates malformed lines
  instead of crashing.
- `RetryChatService` now retries only transient exceptions
  (`HttpRequestException`, `TimeoutException`, `IOException`).
- HTTP error responses now differentiate 429/401/403 with descriptive messages.
- `ToolRegistry` now uses a `SemaphoreSlim` to prevent reconfigure/lookup races.
- `AgentSession` now implements `IDisposable` (unsubscribes events, clears
  history); `ChatTabViewModel` disposes old session on project switch.
- `AgentSession.TrimHistory` now truncates oversized single messages with a
  marker, and history persistence is versioned with backward-compatible
  legacy loading.
- `ChatTabViewModel` assistant content updates now marshaled via
  `Dispatcher.UIThread.Post`.
- `MainWindow` event handler leak and `SolidColorBrush` memory leak fixed;
  `DispatcherTimer` now disposed on window close.
- `CosineSimilarity` now validates inputs for NaN/zero-norm.
- Chunker metadata now returns defensive copies to prevent caller mutation.
- `IndexBuilder` now handles failed embeddings gracefully (skip + warning).
- `InMemoryVectorStore` search snapshot now uses `ToList()` defensive copy.
- `ProjectTools` process kill now has a 1-second fallback re-kill.
- `RefreshFiles` simplified to avoid headless dispatcher deadlock in tests.
- `StreamTimeoutTests` flakiness fixed.
- Path traversal tests extended for double-encoded sequences.
- `ProjectViewModelTests` updated to use async `LoadProjectAsync`.
- `ModCallbacks.cs` ID mapping: all 74 vanilla callback IDs (0-73)
  corrected to match official IsaacDocs documentation. Previous mapping
  was systematically wrong from ID 4 onward.
- `SettingsViewModel` now injects `AppConfiguration` via DI instead of
  calling `AppConfiguration.Load()` directly, eliminating multiple
  instance drift.
- `EmbeddingProviderProxy` now implements `IDisposable` and disposes
  old `OnnxEmbeddingProvider` instances on `Replace()`, fixing an
  `InferenceSession` leak during embedding hot-swapping.
- `SettingsWindow.axaml` text updated to reflect actual embedding
  hot-reload behavior (no restart required).
- `InMemoryVectorStore.Search` now takes a defensive copy of the
  entries list to prevent concurrent mutation during iteration.
- `GetCallbackInfoTool` searches both vanilla and REPENTOGON callback
  dictionaries and displays REPENTOGON override IDs.
- `DiagnoseLuaTool` unknown-callback check extended to cover
  REPENTOGON callbacks, eliminating false "Unknown callback" warnings.
- `ApiDocChunker` generates RAG chunks for REPENTOGON callbacks.
- `QuickReferenceViewModel` lists REPENTOGON callbacks alongside vanilla.

### Changed
- `CommandPaletteViewModel` accepts an optional `SkillRegistry` and derives
  skill entries from it (single source of truth), eliminating the duplicated
  skill list that could drift out of sync with `AgentServiceRegistration`.
- `SkillDescriptor` record now carries `DisplayName` alongside Name,
  Description, and SlashCommand.
- `FileToolPathSafety` changed from `file static class` to `internal static
  class` so it can be shared across tool implementation files.
- CI workflow restructured from single windows-latest job to three jobs:
  `ci-lib`, `ci-windows`, `release`.
- `OpenAICompatibleProvider` and `OllamaProvider` stream loops changed
  from `while (!reader.EndOfStream)` to `while (true)` + null check,
  avoiding synchronous `Read()` calls on async-only streams.
- P3-1 status updated from "partially fixed" to "fully fixed".
- Versioning switched from hardcoded `VersionPrefix`/`AssemblyVersion`/
  `FileVersion` in `Directory.Build.props` to [MinVer](https://github.com/adamralph/minver)
  (Git-tag-based). AssemblyVersion, FileVersion, Version, PackageVersion and
  InformationalVersion are now derived from the latest `v*` tag automatically.
  `MinVerMinimumMajorMinor=0.1` preserves the 0.1.* baseline until the first
  `v0.1.0` tag. CI checkout `fetch-depth` set to `0` in all three jobs so
  MinVer has full git history.
- `SystemPrompts` tool list and guidelines synchronized with `ToolRegistry`:
  added `git_status`, `diff_apply`, `batch_edit`, `run_command` to the
  Available Tools list and 3 new guideline entries.
- README `tools/e2e-test/` note corrected from "not in .sln" to "in solution
  as IsaacAgent.E2ETest".

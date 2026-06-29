# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.6] - 2026-06-29

Project file management: right-click context menu, file search, open
in external editor, and copy path/explorer integration.

### Added

- Right-click context menu on file tree: New File, New Folder,
  Rename, Delete, Copy Path, Open in Explorer, Open in External
  Editor. Commands operate on the right-clicked item (file or
  folder) or the project root if no item is selected.
- File search: a search box above the file tree filters files in
  real-time by name or path. When searching, the TreeView is
  replaced by a flat ListBox of matching files. Case-insensitive.
- Open in External Editor: opens a file using the system default
  application via Process.Start with UseShellExecute.
- Open in Explorer: opens the containing folder in the system file
  explorer.
- Copy Path: copies the full absolute path of a file or folder to
  the clipboard.
- i18n strings for all file management UI elements in 4 languages
  (FileSearchWatermark, FileNewFile, FileNewFolder, FileRename,
  FileDelete, FileCopyPath, FileOpenInExplorer, FileOpenExternal).
- ProjectViewModelFileManagementTests: 11 tests (create file/folder,
  delete file/folder, rename, search match/empty/case-insensitive,
  null parameter safety).

### Changed

- ProjectViewModel: added FileSearchText, HasFileSearch,
  FilteredFiles properties. Added CreateNewFile, CreateNewFolder,
  DeleteFile, RenameFile, CopyPath, OpenInExplorer,
  OpenInExternalEditor commands. Added UpdateFilteredFiles and
  FlattenAllFiles helper methods.
- MainWindow.axaml: file tree area now has a search box, a
  ListBox for search results (shown when searching), and a
  ContextMenu on the TreeView with 7 menu items.

### Tests

- Total: 579 tests (575 pass, 4 skip, 0 fail)

## [0.1.5] - 2026-06-29

Chat interaction enhancements: message copy, edit/resend, stop
generation, and Lua snippet quick-insert.

### Added

- Message copy button: each regular message (user/assistant) has a
  "Copy" button that copies the message content to the clipboard.
- Message edit and resend: user messages have an "Edit" button that
  enters edit mode. After editing, "Resend" removes all subsequent
  messages, updates the user message content, rebuilds the session
  history, and re-sends to get a fresh response.
- Lua snippet quick-insert: a dropdown above the chat input box
  provides 12 common Isaac modding Lua snippets (callbacks, utility
  functions, entity spawning). Selecting a snippet inserts its code
  into the input box. LuaSnippetService provides the static catalog.
- Stop generation button: already existed as CancelCommand, now
  properly labeled with i18n string and visible during generation.
- i18n strings for new UI elements: ChatCopy, ChatEdit, ChatResend,
  ChatInsertSnippet in all 4 languages (en, zh, ja, ko).
- LuaSnippetServiceTests: 9 tests (catalog not empty, all have
  name/code/category, category coverage, name uniqueness).
- ChatTabViewModelTests: +6 tests (InsertSnippet empty/existing/empty-
  snippet, StartEdit user/assistant, CancelEdit).

### Changed

- ChatMessageViewModel: added IsEditing, EditText properties,
  CopyContentCommand, StartEditCommand, CancelEditCommand.
- ChatTabViewModel: added ResendCommand (edit and resend user message),
  InsertSnippetCommand (insert Lua code into input box).
- MainWindow.axaml: message template now includes Copy and Edit buttons
  for regular messages. Edit mode shows a TextBox with Resend/Cancel
  buttons. Snippet dropdown added above chat input.
- MainWindow.axaml.cs: OnSnippetSelected handler inserts snippet code
  and resets dropdown selection.

### Tests

- Total: 568 tests (564 pass, 4 skip, 0 fail)

## [0.1.4] - 2026-06-29

Extended i18n (Japanese/Korean), custom accent color, font size
scaling, chat history persistence/export/search, and Ctrl+F shortcut.

### Added

- Japanese (ja) and Korean (ko) UI translations: 115 string keys each
  in Strings.ja.axaml and Strings.ko.axaml. LocalizationService now
  supports 4 languages (en, zh, ja, ko).
- Custom accent color: user can specify a hex color string in Settings
  to override the theme's default accent color. Applied at runtime via
  ThemeService.ApplyAccentColor. Persisted in AppConfiguration.
- Font size scaling: small (0.85x), medium (1.0x), large (1.15x)
  options via FontSizeService. Persisted in AppConfiguration.
- Chat history persistence: ChatHistoryService saves all tabs and
  messages as JSON per project directory. History is automatically
  saved when switching projects and restored when opening a project.
- Chat export: export active tab as Markdown or JSON via Chat menu.
  Files saved to Documents/IsaacAgentExports/.
- Chat search: Ctrl+F toggles a search panel that searches across all
  chat tabs. Results show tab title and message content preview.
- Ctrl+F keyboard shortcut for toggling search panel.
- ChatHistoryServiceTests: 12 tests (path generation, save/load
  round-trip, delete, export markdown/json, search empty/match/case-
  insensitive/multi-tab).
- FontSizeServiceTests: 9 tests (multiplier mapping, sizes list).
- LocalizationServiceTests: +2 tests (Japanese/Korean constants).
- SettingsViewModelTests: updated for 4 languages.

### Changed

- AppConfiguration: added AccentColor (hex string) and FontSize
  ("small"/"medium"/"large") fields with persistence.
- MainViewModel: added ChatHistory property, SearchQuery, IsSearchVisible,
  ExportChatMarkdown/ExportChatJson/ToggleSearch commands. Project
  switch now saves previous project's history and restores new
  project's history.
- ThemeService: added CustomAccentColor property, ApplyAccentColor
  and ApplyInitialAccentColor methods.
- SettingsWindow: added Accent Color text box and Font Size combo box
  in Appearance section.
- MainWindow: added search panel (toggle with Ctrl+F), export menu
  items in Chat menu.

### Tests

- Total: 553 tests (549 pass, 4 skip, 0 fail)

## [0.1.3] - 2026-06-29

Internationalization, theme switching, new keyboard shortcuts, and
expanded ViewModel test coverage.

### Added

- Multi-language support (i18n): English and Chinese UI with runtime
  switching via LocalizationService. All windows (MainWindow, Settings,
  About, DiffViewer, TemplateGallery, CommandPalette) use
  DynamicResource bindings. Language preference persisted in
  AppConfiguration. 115 string keys in Strings.en.axaml and
  Strings.zh.axaml.
- Light/dark theme switching: ThemeService swaps Avalonia
  RequestedThemeVariant and Isaac-specific color palette at runtime.
  Theme.Light.axaml provides light-appropriate colors for all semantic
  categories (accent, syntax, chat backgrounds, diff, log, markdown,
  toast). Theme preference persisted in AppConfiguration.
- Keyboard shortcuts: Ctrl+Tab (switch to next chat tab), Ctrl+W (close
  active tab), F1 (open About dialog). ChatViewModel.SwitchToNextTab
  command cycles through tabs with wraparound.
- Settings window: language and theme selectors in a new Appearance
  section at the top.
- DiffViewerViewModelTests: 7 tests (constructor, SetProjectDir,
  SelectedFile, RefreshAsync).
- MainViewModelTests: 8 tests (constructor, ClearChat, StatusText,
  IsBusy, DI instance verification).
- SettingsViewModelTests: 14 tests (config loading, properties,
  providers, embeddings, language/theme).
- LocalizationServiceTests: 8 tests (default language, config loading,
  supported languages).
- ThemeServiceTests: 8 tests (default theme, config loading, supported
  themes).
- ChatViewModelTests: +2 SwitchToNextTab tests (single tab no-op,
  multi-tab cycle).

### Changed

- AppConfiguration: added Language ("en"/"zh") and Theme
  ("dark"/"light") fields with persistence.
- MainViewModel: StatusText now loaded from i18n resources instead of
  hardcoded English.
- ChatTabViewModelTests: FlushDispatcher uses CheckAccess guard for
  thread safety in headless test runner.
- CI release workflow: draft: true changed to draft: false so GitHub
  Releases are published automatically on tag push.

### Tests

- Total: 529 tests (525 pass, 4 skip, 0 fail)
- 4 skipped tests are dispatcher-dependent (Dispatcher.UIThread.Post)
  which cannot be reliably flushed in headless test runner when run as
  part of full suite.

## [0.1.2] - 2026-06-28

UX improvements: window state persistence, drag-and-drop, toast
notifications, About dialog as AXAML, and expanded test coverage.

### Added

- Window state persistence: MainWindow size, position, and maximized
  state are saved to AppConfiguration on close and restored on next
  launch. Multi-monitor negative coordinates are handled; sanity guards
  prevent restoring invalid geometry.
- Drag-and-drop support: dropping a folder onto the window opens it as
  a project; dropping a file reads its content and injects it into the
  active chat tab's input box as a fenced code block for LLM context.
- Toast notification system: transient overlay notifications (Info,
  Success, Warning, Error) displayed in the bottom-right corner with
  auto-dismiss. Triggered on project load, index rebuild success/failure,
  and index prewarm failure. Severity-colored icons (ℹ ✓ ⚠ ✗) via
  shared theme brushes.
- AboutWindow.axaml: dedicated AXAML window for the About dialog,
  replacing the inline code-built Window. Version is bound from
  AssemblyInformationalVersion (MinVer).
- Unit tests for QuickReferenceViewModel (9 tests): callbacks, classes,
  mod structure loading, sorting, no duplicates, REPENTOGON callbacks.
- Unit tests for TemplateGalleryViewModel (11 tests): template loading,
  scaffold validation, file creation, placeholder substitution, Lua
  string escaping, default name fallback.
- Unit tests for ToastService (12 tests): show info/success/warning/
  error, multiple toasts, dismiss, notification properties, icon
  mapping, custom duration.
- 4 toast colors added to Theme.axaml (info/success/warning/error).

### Changed

- AppConfiguration.Save() now persists window state fields alongside
  existing LLM/embedding settings.
- MainViewModel exposes ToastService via `Toasts` property for UI
  binding.
- MainWindow.OnAbout simplified to create AboutWindow instead of
  building a Window inline.

## [0.1.1] - 2026-06-28

UI layer improvements: dynamic version display, DI consistency, shared
theme system, Markdown table rendering, recursive file tree, and expanded
test coverage.

### Added

- Markdown table rendering: GFM-style tables (`| col | col |` with
  `|---|---|` separator) are now rendered as monospace, column-aligned
  text with a dashed separator line. Supports auto-sized columns,
  colon-aligned separators, escaped pipes, and missing trailing pipes.
- Shared theme system (`Styles/Theme.axaml`): 24 semantic color and 24
  brush resources centralized in a single resource dictionary, replacing
  30+ hardcoded color values across AXAML and C#.
- Recursive file tree: project files are displayed in a TreeView with
  expandable/collapsible directory nodes instead of a flat list. Build
  artifacts (.git, bin, obj) are excluded; empty directories are skipped;
  top-level directories are expanded by default.
- Unit tests for ChatTabViewModel (16 tests): send/cancel, streaming
  accumulation, error handling, IsGenerating state, ClearMessages,
  OnProjectChanged, ToggleExpand, Dispose.
- Unit tests for ChatViewModel (16 tests): multi-tab management, active
  tab switching, close behavior, project change propagation.
- Unit tests for MarkdownRenderer (20 tests): table rendering, existing
  markdown features regression, edge cases.
- `RenderToText` internal helper on MarkdownRenderer for testability.
- Shared Avalonia test infrastructure (`AvaloniaTestHelper` +
  `AvaloniaFixture` + `AvaloniaTestCollection`): serializes
  Avalonia-dependent tests via xUnit collection fixture to fix
  thread-safety issues with StyledElement static constructors under
  parallel test execution.

### Changed

- About dialog now reads the version from `AssemblyInformationalVersion`
  (set by MinVer from git tags) instead of a hardcoded string.
- `TemplateGalleryViewModel` is now registered in the DI container and
  resolved via `GetRequiredService`, matching the pattern used by all
  other view models.
- All hardcoded color values in AXAML views, converters,
  ChatMessageViewModel, and MarkdownRenderer now resolve brushes via
  `Application.Current.Resources` with semantic resource keys.
- File tree double-click only opens files (not directories) for preview.
- `OnFileDoubleTapped` handles both TreeView and ListBox sources.

### Fixed

- Avalonia headless test initialization race condition: multiple test
  classes initializing the headless application via static constructors
  could race under parallel test execution, causing
  `StyledElement..cctor` failures. Consolidated into a single
  collection fixture.

## [0.1.0] - 2026-06-28

First public release. An AI coding agent for The Binding of Isaac: Repentance
Lua mod development, built with .NET 8 + Avalonia 11.

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
- Agent loop end-to-end tests (`AgentSessionE2ETests`): 12 tests using a
  scripted fake LLM (`ScriptedChatService` returns different chunks per
  iteration) and verifiable fake tools (`FakeTool` records invocations).
  Covers multi-turn tool chains (A→B→final), parallel tool calls, argument
  passing, tool result feedback to LLM, tool result sanitization boundary
  markers, throwing tool error handling, event firing (OnToolCall/
  OnToolResult/OnTextGenerated), and skill activation (slash command
  stripping, auto-activation, pre-fetched context injection). No real LLM
  or Ollama dependency — runs in CI.
- Knowledge-base accuracy guard tests (`ModCallbacksAccuracyGuardTests`):
  parse the embedded `ModCallbacks.md` tables and assert that every C#
  callback ID matches the official documentation. Covers vanilla callbacks
  (bidirectional exact match, all 74 IDs) and REPENTOGON callbacks
  (one-directional: every C# entry must appear in the markdown). Prevents
  recurrence of KB-1, a systematic ID mapping error.
- Cross-platform CI matrix: `ci-lib` (ubuntu-latest, macos-latest) for
  library tests, `ci-windows` for full solution build + format check.
- Release build job: publishes self-contained win-x64 single-file
  executable on `v*` tag pushes, creates a draft GitHub Release.
- Nuke `CiLib` and `UnitTestLib` targets for cross-platform library testing.
- Nuke `Publish`, `PublishVerify`, `Release`, and `Test` targets. Publish
  is now orchestrated by Nuke instead of raw `dotnet publish` in CI YAML.
  New parameters: `Version` (override MinVer), `Runtime` (default `win-x64`).
- Code coverage collection via `XPlat Code Coverage` data collector in all
  test targets. CI jobs upload coverage artifacts.
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
- Output directory standardized to `artifacts/publish/{Runtime}/`
  (was ad-hoc `publish/`).

### Fixed

- `MC_HUD_UPDATE` (ID 1020) and `MC_HUD_POST_UPDATE` (ID 1021) moved from
  `RepentogonModifiedIds` to `RepentogonCallbacks`. They are REPENTOGON-only
  callbacks (not vanilla overrides) and were misclassified.
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

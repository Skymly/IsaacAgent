# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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
- Comprehensive agent tools table in README with module attribution.

### Fixed
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
- CI workflow restructured from single windows-latest job to three jobs:
  `ci-lib`, `ci-windows`, `release`.
- `OpenAICompatibleProvider` and `OllamaProvider` stream loops changed
  from `while (!reader.EndOfStream)` to `while (true)` + null check,
  avoiding synchronous `Read()` calls on async-only streams.
- P3-1 status updated from "partially fixed" to "fully fixed".

# IsaacAgent

[![CI](https://github.com/Skymly/IsaacAgent/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/Skymly/IsaacAgent/actions/workflows/build-and-test.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)
![Avalonia](https://img.shields.io/badge/Avalonia-11-01A0E9.svg)
![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)

An AI coding agent for **The Binding of Isaac: Repentance** Lua mod development.
It provides a Cursor/OpenCode-like desktop experience with Isaac-specific
knowledge, tools, and workflows built in.

<!-- TODO: Add screenshot here once available -->
<!-- ![IsaacAgent Screenshot](docs/screenshot.png) -->

## Quick Start

### 1. Get the App

**Option A — Build from source** (requires .NET 8 SDK):

```powershell
git clone https://github.com/Skymly/IsaacAgent.git
cd IsaacAgent
dotnet build IsaacAgent.sln -c Release
dotnet run --project src/IsaacAgent.App/IsaacAgent.App.csproj -c Release
```

**Option B — Publish a single-file executable**:

```powershell
./build.ps1 --target Publish --configuration Release
.\artifacts\publish\win-x64\IsaacAgent.exe
```

### 2. Configure LLM

On first launch, open **Settings** (Ctrl+,) and configure:

| Field | Example | Notes |
|-------|---------|-------|
| Provider | OpenAI-compatible / Ollama | OpenAI-compatible works with any endpoint |
| Endpoint | `https://api.minimax.chat/v1` | Or `http://localhost:11434` for Ollama |
| Model | `abab6.5s-chat` / `llama3` | Depends on your provider |
| API Key | `your-key-here` | Stored encrypted with DPAPI |

For embeddings (RAG), **ONNX is the default** — a bundled
`all-MiniLM-L6-v2` model ships with the app (no extra download). You can
switch to **Ollama** in Settings if you prefer a remote embedder.

### 3. Start Coding

1. **Ctrl+N** — New project (or **Ctrl+Shift+T** to pick a template)
2. Type your request in the chat box, e.g. *"Create a passive item that doubles fire rate"*
3. The agent will scaffold files, write code, and validate XML automatically
4. **Ctrl+Enter** to send, **Ctrl+K** to clear chat

## Features

### Core

- **Chat-driven mod development** — streaming assistant responses with
  tool-calling for file operations, scaffolding, diagnostics, API lookup,
  and semantic knowledge search.
- **Isaac API knowledge base** — built-in enums, classes, and callbacks for
  the vanilla API (74 callbacks) plus REPENTOGON extensions (130+ callbacks).
- **Local RAG search** — semantic search over 480+ embedded modding docs
  (MkDocs chunker + **bundled ONNX** all-MiniLM-L6-v2 by default, or Ollama;
  in-memory vector store with on-disk index cache).
- **Multi-provider LLM** — any OpenAI-compatible endpoint (MiniMax, OpenAI,
  local servers) or Ollama.
- **Project-aware tools** — file operations are sandboxed to the opened
  project directory; XML/Lua escaping and path-traversal guards are built in.
- **Encrypted credential storage** — API keys are protected with DPAPI
  (CurrentUser scope) on Windows.
- **Persistent chat history** — per-project history is saved and restored
  across restarts.

### UI Features

| Feature | Shortcut | Description |
|---------|----------|-------------|
| Multi-tab chat | — | Independent conversation contexts per tab |
| Command palette | Ctrl+Shift+P | Fuzzy-search all app actions |
| Template gallery | Ctrl+Shift+T | 5 built-in mod templates (Basic, Item, Familiar, Challenge, Save Data) |
| Live log monitor | — | Real-time Isaac `log.txt` parsing with error highlighting |
| Visual diff viewer | Ctrl+Shift+D | Side-by-side git diff with color-coded changes |
| Quick reference panel | — | Browse callbacks, classes, and mod structure |
| Settings | Ctrl+, | LLM and embedding provider configuration |

### Agent Tools

The agent has access to 16 tools across two modules:

**IsaacAgent.Tools** (file operations, scaffolding, API knowledge, project commands):

| Tool | Description |
|------|-------------|
| `read_file` | Read a file from the project directory |
| `write_file` | Write a file (creates parent dirs) |
| `list_files` | List all files in the project directory |
| `scaffold_mod` | Create a new mod project structure |
| `diagnose_lua` | Analyze Lua for common Isaac modding pitfalls |
| `search_isaac_api` | Search API docs (classes, callbacks, enums) |
| `get_callback_info` | Detailed info on a specific callback |
| `get_class_info` | Detailed info on a specific API class |
| `git_status` | Show git status, recent commits, and diff |
| `diff_apply` | Apply a unified diff patch to a file |
| `batch_edit` | Apply multiple find-and-replace edits across files |
| `run_command` | Run a shell command in the project directory |

**IsaacAgent.Rag** (semantic search, validation, log parsing):

| Tool | Description |
|------|-------------|
| `search_knowledge` | Semantic search over the knowledge base |
| `get_pattern` | Find code patterns for common tasks |
| `validate_xml` | Validate XML against official XSD schemas (35 schemas) |
| `parse_log` | Extract errors/warnings from `log.txt` |

### Skills

Skills are higher-level workflows that augment the agent's behavior for specific
Isaac modding tasks. They sit between the system prompt and atomic tools:
each skill injects task-specific guidance and pre-fetches relevant RAG context
before the LLM processes the request. Skills activate automatically when the
user's message matches a keyword pattern, or explicitly via a slash command
(type `/` in the chat box or pick one from the command palette).

| Skill | Slash command | Description |
|-------|---------------|-------------|
| Create Collectible | `/create-item` | Custom passive/active item with callbacks, items.xml, validation |
| Create Familiar | `/create-familiar` | Companion with orbit/follow/shoot behavior, entities2.xml |
| Debug from Log | `/debug` | Parse log.txt, diagnose Lua, propose a fix |
| Validate Project | `/validate` | Validate all XML against schemas and run Lua diagnostics |
| Add Callback | `/add-callback` | Add a specific Isaac callback to main.lua |
| Add Save Data | `/add-save-data` | Persistent save data via SaveModData/LoadModData |
| Add Trinket | `/add-trinket` | Custom trinket with pocket-active effect and pickup metadata |
| Add Card / Rune | `/add-card` | Custom card or rune with use effect and pickup metadata |
| Add Pill | `/add-pill` | Custom pill effect with pill pool registration |
| Add Boss | `/add-boss` | Custom boss with AI, attacks, boss room spawning, portrait |

### Knowledge Base

- **480+ Markdown docs** — vanilla + REPENTOGON API documentation
- **25 code patterns** — collectible (passive/active), familiar (basic + advanced), boss, room, challenge, character, trinket, card/pill, curse, door, pedestal, shop, devil room, tear effect, HUD, music, cutscene, status effect, save data, achievement tracking, item pool modification, multiplayer sync, REPENTOGON ImGui menu
- **35 XSD schemas** — official Isaac XML validation schemas
- **74 vanilla callbacks** with canonical IDs (0-73)
- **130+ REPENTOGON callbacks** (IDs 1000+) with override tracking
- **Isaac classes** with method listings
- **Enums** with value descriptions
- **5 few-shot workflows** — create collectible, debug runtime error, validate project, add save data, create familiar (embedded in system prompt to guide multi-step tool orchestration)

## Tech Stack

- .NET 8 + Avalonia 11 + CommunityToolkit.Mvvm
- MVVM architecture, multi-project solution
  (App / Core / LLM / Tools / Agent / Rag)

## Prerequisites

- .NET 8 SDK
- Windows (the UI targets `windows` as its supported OS platform)
- An LLM API endpoint (OpenAI-compatible or Ollama)

### Configuration

Set environment variables or create a config file:

```bash
# Environment variables — ISAAC_AGENT_API_KEY is required to use env-based config.
# ENDPOINT and MODEL fall back to defaults if omitted.
set ISAAC_AGENT_API_KEY=your-api-key
set ISAAC_AGENT_ENDPOINT=https://api.minimax.chat/v1
set ISAAC_AGENT_MODEL=abab6.5s-chat

# Or for Ollama
set ISAAC_AGENT_ENDPOINT=http://localhost:11434
set ISAAC_AGENT_MODEL=llama3
```

Or place a `config.json` in `%APPDATA%\IsaacAgent\`. The API key is
encrypted with DPAPI (CurrentUser scope) and stored in `EncryptedApiKey`:

```json
{
  "ProviderType": "OpenAICompatible",
  "Endpoint": "https://api.minimax.chat/v1",
  "Model": "abab6.5s-chat",
  "EncryptedApiKey": "<base64 DPAPI ciphertext>"
}
```

The config file is normally managed by the Settings UI; you rarely need
to edit it by hand. If you do, leave `EncryptedApiKey` empty and set the
key via environment variable or the Settings dialog.

### Build & Run

```powershell
# Via Nuke (CI-authoritative)
./build.ps1 --target Ci --configuration Release

# Or traditional dotnet
dotnet build IsaacAgent.sln -c Release
dotnet run --project src/IsaacAgent.App/IsaacAgent.App.csproj -c Release
```

### Tests

```powershell
# Via Nuke
./build.ps1 --target Ci --configuration Release

# Or traditional dotnet
dotnet test IsaacAgent.sln
```

### Publish a Single-File Executable

```powershell
# Via Nuke (CI-authoritative, includes verification)
./build.ps1 --target Publish --configuration Release --runtime win-x64

# Or traditional dotnet
dotnet publish src/IsaacAgent.App/IsaacAgent.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish/win-x64
```

### Versioning

Versions are derived automatically from Git tags by [MinVer](https://github.com/adamralph/minver).
Pushing a `v*` tag (e.g. `v0.1.0`) triggers the CI release job and sets the
assembly/package version. Between tags, the version auto-increments as
pre-release (e.g. `0.1.0-alpha.0.42` where `42` is the commit count since the
last tag). No manual version editing is needed.

```bash
git tag v0.1.0
git push origin v0.1.0   # triggers CI release job
```

## Project Layout

```
src/
  IsaacAgent.App/       Avalonia UI (Views, ViewModels, DI)
  IsaacAgent.Core/      Domain models, interfaces, knowledge base
  IsaacAgent.LLM/       LLM provider abstraction (OpenAI-compatible, Ollama)
  IsaacAgent.Tools/     Agent tools (file ops, scaffolding, diagnostics, API search)
  IsaacAgent.Rag/       RAG pipeline (chunking, embedding, retrieval, XML validation)
  IsaacAgent.Agent/     Agent engine (orchestration, tool routing, prompts)
tests/
  IsaacAgent.Tests/     Unit and integration tests
tools/
  e2e-test/             Standalone E2E test for RAG pipeline (in solution as IsaacAgent.E2ETest)
build/                  Nuke build script
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, code
conventions, and pull request guidelines.

## License

[MIT](LICENSE) — Copyright (c) 2026 Skymly

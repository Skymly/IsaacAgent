# IsaacAgent

An AI coding agent for **The Binding of Isaac: Repentance** Lua mod development.
It provides a Cursor/OpenCode-like desktop experience with Isaac-specific
knowledge, tools, and workflows built in.

## Features

- **Chat-driven mod development** — streaming assistant responses with
  tool-calling for file operations, scaffolding, diagnostics, API lookup,
  and semantic knowledge search.
- **Isaac API knowledge base** — built-in enums, classes, and callbacks for
  the vanilla API plus REPENTOGON extensions.
- **Local RAG search** — semantic search over embedded modding documentation
  (MkDocs chunker + ONNX or Ollama embeddings, in-memory vector store).
- **Multi-provider LLM** — any OpenAI-compatible endpoint (MiniMax, OpenAI,
  local servers) or Ollama.
- **Project-aware tools** — file operations are sandboxed to the opened
  project directory; XML/Lua escaping and path-traversal guards are built in.
- **Encrypted credential storage** — API keys are protected with DPAPI
  (CurrentUser scope) on Windows.
- **Persistent chat history** — per-project history is saved and restored
  across restarts.

### Agent Tools

The agent has access to 16 tools across two modules:

**IsaacAgent.Tools** (file operations, scaffolding, API knowledge, project commands, 12 tools):

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
| `validate_xml` | Validate XML against official XSD schemas |
| `parse_log` | Extract errors/warnings from `log.txt` |

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

```bash
dotnet publish src/IsaacAgent.App/IsaacAgent.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
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
  e2e-test/             Standalone E2E test for RAG pipeline (not in .sln)
build/                  Nuke build script
```

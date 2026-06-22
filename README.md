# IsaacAgent

An AI coding agent for **The Binding of Isaac: Repentance** Lua mod development.
It provides a Cursor/OpenCode-like desktop experience with Isaac-specific
knowledge, tools, and workflows built in.

## Features

- **Chat-driven mod development** — streaming assistant responses with
  tool-calling (read/write files, scaffold mods, diagnose Lua, validate XML,
  parse game logs).
- **Isaac API knowledge base** — built-in enums, classes, and callbacks for
  the vanilla API plus REPENTOGON extensions.
- **Local RAG search** — semantic search over embedded modding documentation
  (MkDocs chunker + ONNX or Ollama embeddings, in-memory vector store).
- **Multi-provider LLM** — any OpenAI-compatible endpoint (MiniMax, OpenAI,
  local servers) or Ollama.
- **Project-aware tools** — file operations are sandboxed to the opened
  project directory; XML/Lua escaping and path-traversal guards are built in.
- **Isaac-specific diagnostics** — `diagnose_lua` catches common pitfalls
  (missing `local`, unbalanced braces, unescaped strings, `debug 7` litter).
- **Schema validation** — `validate_xml` checks `metadata.xml`, `items.xml`,
  `entities2.xml`, etc. against official XSD schemas.
- **Log parsing** — `parse_log` extracts Lua errors and warnings from the
  game's `log.txt`.
- **Encrypted credential storage** — API keys are protected with DPAPI
  (CurrentUser scope) on Windows.
- **Persistent chat history** — per-project history is saved and restored
  across restarts.

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
  IsaacAgent.Tools/     Agent tools (file ops, scaffolding, diagnostics)
  IsaacAgent.Rag/       RAG pipeline (chunking, embedding, retrieval)
  IsaacAgent.Agent/     Agent engine (orchestration, tool routing, prompts)
tests/
  IsaacAgent.Tests/     Unit and integration tests
```

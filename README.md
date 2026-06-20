# IsaacAgent

AI coding agent for **The Binding of Isaac: Repentance** Lua mod development.

Built with **Avalonia + C# / .NET 8**, inspired by Cursor and OpenCode.

## Features

- **AI Chat Interface** — Conversational coding assistant that understands Isaac modding
- **Isaac API Knowledge Base** — Built-in documentation for all callbacks, classes, enums, and constants
- **Code Generation** — Generate Lua code, mod structures, XML configs
- **Code Diagnostics** — Detect common Isaac modding mistakes (wrong callbacks, API misuse, syntax errors)
- **Project Scaffolding** — Create complete mod project structures with one command
- **File Management** — Read, write, and browse mod project files
- **Multi-Provider LLM** — Supports OpenAI-compatible APIs (MiniMax, DeepSeek, etc.) and local Ollama

## Project Structure

```
src/
  IsaacAgent.App/       — Avalonia UI (Views, ViewModels, DI)
  IsaacAgent.Core/      — Domain models, interfaces, knowledge base
  IsaacAgent.LLM/       — LLM provider abstraction (OpenAI-compatible, Ollama)
  IsaacAgent.Tools/     — Agent tools (file ops, scaffolding, diagnostics, API search)
  IsaacAgent.Agent/     — Agent engine (orchestration, tool routing, prompts)
tests/
  IsaacAgent.Tests/     — Unit tests
```

## Getting Started

### Prerequisites

- .NET 8 SDK
- An LLM API endpoint (OpenAI-compatible or Ollama)

### Configuration

Set environment variables or create a config file:

```bash
# Environment variables
set ISAAC_AGENT_API_KEY=your-api-key
set ISAAC_AGENT_ENDPOINT=https://api.minimax.chat/v1
set ISAAC_AGENT_MODEL=abab6.5s-chat

# Or for Ollama
set ISAAC_AGENT_ENDPOINT=http://localhost:11434
set ISAAC_AGENT_MODEL=llama3
```

Or place a `config.json` in `%APPDATA%\IsaacAgent\`:

```json
{
  "ProviderType": "OpenAICompatible",
  "Endpoint": "https://api.minimax.chat/v1",
  "Model": "abab6.5s-chat",
  "ApiKey": "your-key"
}
```

### Build & Run

```bash
dotnet build
dotnet run --project src/IsaacAgent.App
```

## Available Agent Tools

| Tool | Description |
|------|-------------|
| `read_file` | Read a file from the mod project |
| `write_file` | Write/create a file in the mod project |
| `list_files` | List all files in the project |
| `search_isaac_api` | Search Isaac modding API documentation |
| `get_callback_info` | Get detailed info about a specific callback |
| `get_class_info` | Get detailed info about a specific class |
| `diagnose_lua` | Analyze a Lua file for common issues |
| `scaffold_mod` | Create a new mod project structure |

## Tech Stack

- .NET 8 / C# 12
- Avalonia 11 (cross-platform desktop UI)
- CommunityToolkit.Mvvm (MVVM pattern)
- Microsoft.Extensions.DependencyInjection
- xUnit (testing)

# IsaacAgent — AI Coding Agent for Binding of Isaac: Repentance Modding

## Overview

IsaacAgent is a desktop AI coding agent (Avalonia + C#) specialized for **The Binding of Isaac: Repentance** Lua mod development. It provides Cursor/OpenCode-like experience with Isaac-specific knowledge, tools, and workflows.

## Tech Stack

- **.NET 8** + **Avalonia 11** + **CommunityToolkit.Mvvm**
- **MVVM** architecture
- Multi-project solution: App / Core / LLM / Tools / Agent / Rag

## Project Structure

```
src/
  IsaacAgent.App/       — Avalonia UI (Views, ViewModels, DI)
  IsaacAgent.Core/      — Domain models, interfaces, knowledge base
  IsaacAgent.LLM/       — LLM provider abstraction (OpenAI-compatible, Ollama)
  IsaacAgent.Tools/     — Agent tools (file ops, scaffolding, diagnostics, API search)
  IsaacAgent.Agent/     — Agent engine (orchestration, tool routing, prompts)
  IsaacAgent.Rag/       — RAG pipeline (chunking, embedding, retrieval)
tests/
  IsaacAgent.Tests/     — Unit tests
build/                  — Nuke build script (_build.csproj + Program.cs)
.nuke/                  — Nuke parameters
```

## Build & CI (Nuke)

Build orchestration uses [Nuke](https://nuke.build). The CI workflow calls
Nuke targets; the same commands run locally.

| Nuke target | Description |
|-------------|-------------|
| **Ci** | `Clean` → `Restore` → `Compile` → `UnitTest` |
| **CiLib** | Cross-platform library tests only (no App project) |
| **CiAll** | `Format` + `Ci` (full local/CI verification) |
| **Test** | Alias for `UnitTest` |
| **Format** | `dotnet format --verify-no-changes` (fails if formatting needed) |
| **FormatFix** | `dotnet format` (applies formatting in-place) |
| **Publish** | Self-contained single-file exe → `artifacts/publish/{Runtime}/` |
| **PublishVerify** | `Publish` + verify exe exists and size >50 MB |
| **Release** | `CiAll` + `PublishVerify` (full release pipeline) |

Parameters: `--configuration`, `--runtime` (default `win-x64`), `--version` (override MinVer)

```powershell
# CI-equivalent (what the workflow runs)
./build.ps1 --target CiAll --configuration Release

# Quick local build + test
./build.ps1 --target Ci

# Check formatting without changing files
./build.ps1 --target Format

# Apply formatting
./build.ps1 --target FormatFix

# Publish self-contained executable
./build.ps1 --target Publish --configuration Release --runtime win-x64

# Full release pipeline (verify + publish + verify output)
./build.ps1 --target Release --configuration Release
```

Equivalent dotnet CLI:

```powershell
dotnet run --project build/_build.csproj -- --target CiAll --configuration Release
```

Traditional dotnet commands still work but are not the CI-authoritative path:

```powershell
dotnet build IsaacAgent.sln -c Release
dotnet test IsaacAgent.sln -c Release
```

## Conventions

- C# 12 features allowed
- `async/await` for all I/O
- File-scoped namespaces
- Nullable reference types enabled
- No AI tool/model attribution in commits

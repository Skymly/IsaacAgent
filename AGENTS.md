# IsaacAgent — AI Coding Agent for Binding of Isaac: Repentance Modding

本文件为在本仓库工作的 AI 编码助手提供上下文。修改代码前请先阅读本文档与 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)；文档约定见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)。

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
| **CiAll** | `Format` + `Ci` (full local/CI verification) |
| **Test** | Alias for `UnitTest` |
| **Format** | `dotnet format --verify-no-changes` (fails if formatting needed) |
| **FormatFix** | `dotnet format` (applies formatting in-place) |
| **Publish** | Self-contained win-x64 single-file exe → `artifacts/publish/win-x64/` |
| **PublishVerify** | `Publish` + verify exe exists and size >50 MB |
| **Release** | `CiAll` + `PublishVerify` (full release pipeline) |

Parameters: `--configuration`, `--runtime` (default / expected `win-x64`), `--version` (override MinVer)

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

## 跨模块 PR / Issue 边界

与 `IsaacAgent.sln` 一致；**每个模块单独 Issue + PR**（勿在同一 PR 混合多个模块）：

| 模块 | 范围 |
|------|------|
| **App** | `src/IsaacAgent.App/` |
| **Core** | `src/IsaacAgent.Core/` |
| **LLM** | `src/IsaacAgent.LLM/` |
| **Tools** | `src/IsaacAgent.Tools/` |
| **Agent** | `src/IsaacAgent.Agent/` |
| **Rag** | `src/IsaacAgent.Rag/` |
| **Tests** | `tests/IsaacAgent.Tests/`（随所测模块同 PR；纯测试整改可独立 PR） |
| **Docs** | 本仓 `docs/`（维护者文档）；**非** `src/IsaacAgent.Rag/Resources/`（产品知识，随 Rag 模块 PR） |
| **Repository** | 根 `README.md`、`CONTRIBUTING.md`、`AGENTS.md`、`CHANGELOG.md`、`.github/`、`build/` |

跨模块且影响架构时，先记 ADR（如需），再按模块拆多个 Issue → PR。

---

## 文档体系

仓内只保留有沉淀价值的文档；讨论与任务用 GitHub Issue / PR。完整约定见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)。

| 载体 | 位置 | 用途 |
|------|------|------|
| **ADR** | `docs/adr/` | 架构决策（不可变；编号不复用；仅 Supersede） |
| **Design Doc** | `docs/design/` | 每模块一份：API 面 + 不变量 + 实现 + 权衡 |
| **Roadmap** | `docs/ROADMAP.md` | 宏观 backlog |
| **Issue / PR / Release** | GitHub | 需求、审查、版本历史 |

| Design Doc | 模块 |
|------------|------|
| [Agent.md](docs/design/Agent.md) | `IsaacAgent.Agent` |
| [Tools.md](docs/design/Tools.md) | `IsaacAgent.Tools` + RAG 工具 |
| [Rag.md](docs/design/Rag.md) | `IsaacAgent.Rag` |
| [App.md](docs/design/App.md) | `IsaacAgent.App` |

**产品知识 vs 维护者文档**：`docs/` 供维护者；`src/IsaacAgent.Rag/Resources/` 为 Isaac API 嵌入知识，不纳入本体系。

### Agent 文档工作流

| 场景 | Agent 行为 |
|------|-----------|
| 新增 / 变更工具、Skill、API、实现细节 | 更新对应 Design Doc |
| 破坏性架构决策 | 新建 ADR（编号取 `docs/adr/README.md` 下一个可用）+ 更新 Design Doc |
| 用户可见行为 | CHANGELOG `[Unreleased]` 添加条目 |
| 宏观优先级 | 更新 `docs/ROADMAP.md` |
| 文档目录 | 维护者文档仅在 `docs/`；产品知识仅在 `Resources/` |

---

## Git / Issue / PR / Commit

- **语言（权威）**：Issue / PR / Commit **一律英语**；与用户对话默认**简体中文**。本条为权威表述，**覆盖** `docs/DEVELOPMENT.md`、`CONTRIBUTING.md` 中任何「中英文均可」的旧措辞。
- 分支：功能 `feature/<short-description>`、修复 `fix/<short-description>`；提交信息祈使句、说明 **why**。
- **每个 PR 只改一个模块**（边界见上文「跨模块 PR / Issue 边界」）。
- Issue 模板：[`.github/ISSUE_TEMPLATE/`](.github/ISSUE_TEMPLATE/)。
- PR 模板：[`.github/PULL_REQUEST_TEMPLATE.md`](.github/PULL_REQUEST_TEMPLATE.md)（含文档 checklist）。
- **禁止**在 Commit / PR / Issue 中提及 AI / Agent 工具名称。

---

## 澄清与规范

Agent 行为准则——与「与用户沟通」并行生效：

1. **用户表述不清楚时，立刻询问**：不要基于猜测继续工作。用聚焦的问题（而非开放式提问）澄清意图，提供 2–4 个具体选项供用户选择。
2. **用户表述不合理时，立刻指出并给出建议**：包括但不限于——违反已有 ADR（如本地 RAG 改云端向量库、Windows-only 改跨平台发布、Skill 层绕过直接硬编码工具逻辑）、变更工具 schema / Skill 契约却不更新 Design Doc、绕过路径安全（`FileToolPathSafety`、log 白名单）、单 PR 混合多个模块、跳过测试或 Avalonia 测试误用 `[Fact]`（须 `[AvaloniaFact]`）、在 Commit 中写入 API Key / 凭据、在对外文档提及 AI 工具名称、把维护者文档写入 `Resources/` 或把产品知识写入 `docs/`。指出问题时必须说明**为什么不合理**，并给出合理替代方案。
3. **不要盲目执行**：即使能「做到」用户要求的事，如果认为方向有误，应先提出异议，等待用户确认后再动手。
4. **发现矛盾时主动报告**：如果用户的新要求与已有 ADR / Design Doc / `AGENTS.md` 规则冲突，指出冲突点，由用户决定是否更新规则或调整需求（ADR 变更须走 Supersede 流程，见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)）。

## 与用户沟通

- **最小 diff**、匹配现有风格；**不主动** `commit` / `push` / 发版，除非用户明确要求。
- 中文解释权衡；代码标识符与对外文档（Issue / PR / Commit）默认英语。

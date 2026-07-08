# IsaacAgent — AI Coding Agent for Binding of Isaac: Repentance Modding

本文件为在本仓库工作的 AI 编码助手提供上下文。修改代码前请先阅读本文档与 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)；文档驱动流程见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)。

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

跨模块且影响架构时，先 RFC → ADR，再按模块拆多个 Issue → PR。

---

## 文档体系（文档驱动开发）

本仓库实行**文档驱动开发**：先文档后代码，任何非琐碎变更先满足文档前置条件（决策表见 [docs/DOCUMENTATION.md §11](docs/DOCUMENTATION.md#11-文档驱动开发流程)）再进入实现。文档分为 7 种类型，完整规范见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)。Agent 与人类开发者均须遵守。

| 类型 | 目录 | 用途 | 关键规则 |
|------|------|------|----------|
| **RFC** | `docs/rfc/` | 设计提案与讨论 | 新增工具/Skill/破坏性 API/跨模块架构必须 RFC；模板 `docs/rfc/_template.md`；已实现移入 `archive/` |
| **ADR** | `docs/adr/` | 架构决策记录（不可变） | RFC Accepted → 产出 ADR；编号不复用；正文不修改，仅 Supersede |
| **Spec** | `docs/spec/` | 稳定契约（工具 schema、Skill 清单、不变量） | 变更需 RFC + ADR；随代码 PR 同步更新 |
| **Design Doc** | `docs/design/` | 实现细节、设计权衡、已知局限 | 随代码 PR 同步更新 |
| **Plan** | `docs/plans/` | 大型任务计划（跨多 PR） | 里程碑对齐单模块 PR 边界；Done/Cancelled 移入 `archive/`；小任务用 Issue 即可 |
| **Review** | `docs/review/` | 评审记录（设计/实现/发版/回顾） | Final 后正文不可变；行动项全部关闭移入 `archive/` |
| **Roadmap** | `docs/ROADMAP.md` | 功能与技术 backlog | 完成项移入「已完成（归档）」章节 |

**归档统一规则**：归档 = 移动文件 + 更新状态字段 + 更新 README 索引，同一 PR 完成；归档后正文不再修改（仅修失效链接）；归档不删除。

### Agent 文档工作流约定

| 场景 | Agent 行为 |
|------|-----------|
| 新增 Agent 工具 / Skill | 确认是否有对应 RFC + ADR；无则提示需创建 RFC |
| 修改公共 API（`ITool` / `ISkill` / 配置契约） | 确认是否有对应 RFC + ADR；无则提示需创建 RFC |
| 跨多 PR 的大型任务 | 确认 `docs/plans/` 是否有对应 Plan；无则先建 Plan（经用户确认）再实现 |
| 创建 RFC | 使用 `docs/rfc/_template.md`；frontmatter 从 `Draft` 开始 |
| 创建 ADR | 编号取 `docs/adr/README.md` 中下一个可用编号 |
| 创建 Plan / Review | 使用对应 `_template.md`；Review 评审人注明为 Agent |
| RFC / Plan / Review 状态变更 | 更新 frontmatter `状态` + 日期；归档时移动到对应 `archive/` 并更新 README 索引 |
| Spec 变更 | 确认 RFC 已 Accepted；同步更新 Spec 版本号 |
| Design Doc 变更 | 随代码 PR 同步更新 |
| CHANGELOG | 在 `[Unreleased]` 下添加条目 |
| 文档目录 | 不在 `docs/` 之外创建维护者文档（产品知识仅在 `src/IsaacAgent.Rag/Resources/`） |

### 子系统文档结构

子系统文档已拆分为 Spec（`docs/spec/`）+ Design Doc（`docs/design/`）。Spec 描述稳定契约（工具 schema、Skill 清单、不变量），Design Doc 描述实现细节（设计权衡、已知局限）。索引见 [docs/spec/README.md](docs/spec/README.md) 和 [docs/design/README.md](docs/design/README.md)。

| 子系统 | Spec | Design Doc |
|--------|------|------------|
| Agent | [spec/Agent.md](docs/spec/Agent.md) | [design/Agent.md](docs/design/Agent.md) |
| Tools | [spec/Tools.md](docs/spec/Tools.md) | [design/Tools.md](docs/design/Tools.md) |
| Rag | [spec/Rag.md](docs/spec/Rag.md) | [design/Rag.md](docs/design/Rag.md) |
| App | — | [design/App.md](docs/design/App.md) |

**产品知识 vs 维护者文档**：`docs/` 供维护者与贡献者；`src/IsaacAgent.Rag/Resources/` 为 Isaac API / Mod 模式 / XSD 嵌入知识，随 RAG 管线发布，**不**纳入 `docs/` 七层体系。

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
2. **用户表述不合理时，立刻指出并给出建议**：包括但不限于——违反已有 ADR（如本地 RAG 改云端向量库、Windows-only 改跨平台发布、Skill 层绕过直接硬编码工具逻辑）、未走 RFC/ADR 流程变更工具 schema 或 Skill 契约、绕过路径安全（`FileToolPathSafety`、log 白名单）、单 PR 混合多个模块、跳过测试或 Avalonia 测试误用 `[Fact]`（须 `[AvaloniaFact]`）、在 Commit 中写入 API Key / 凭据、在对外文档提及 AI 工具名称、把维护者文档写入 `Resources/` 或把产品知识写入 `docs/`。指出问题时必须说明**为什么不合理**，并给出合理替代方案。
3. **不要盲目执行**：即使能「做到」用户要求的事，如果认为方向有误，应先提出异议，等待用户确认后再动手。
4. **发现矛盾时主动报告**：如果用户的新要求与已有 ADR / RFC / Spec / `AGENTS.md` 规则冲突，指出冲突点，由用户决定是否更新规则或调整需求（ADR 变更须走 Supersede 流程，见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)）。

## 与用户沟通

- **最小 diff**、匹配现有风格；**不主动** `commit` / `push` / 发版，除非用户明确要求。
- 中文解释权衡；代码标识符与对外文档（Issue / PR / Commit）默认英语。

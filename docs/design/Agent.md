# Design Doc: Agent

> **关联 ADR**：[ADR-001](../adr/ADR-001-multi-project-layered-architecture.md)、[ADR-004](../adr/ADR-004-skill-layer-above-tools.md)

## 概述

`IsaacAgent.Agent` 实现 LLM 会话编排：System Prompt 组装、Skill 激活、Tool Calling 循环、流式输出与历史裁剪。

## 设计目标

- 将 Isaac 领域知识通过 Skill + RAG 预取注入，减少用户提示工程
- 保持原子工具可组合、可测试
- 单 Tab 单会话，切换项目时正确 Dispose

## API 面

### 核心接口

| 接口 | 项目 | 职责 |
|------|------|------|
| `IAgentSessionFactory` | `IsaacAgent.Agent` | 创建带项目上下文的 `AgentSession` |
| `ISkill` | `IsaacAgent.Core` | Skill 契约（激活、prompt 增强、RAG 预取） |
| `ITool` | `IsaacAgent.Core` | 原子工具契约（由 `ToolRegistry` 注册） |

### Skill 清单（10）

| Name | Slash 命令 | 用途 |
|------|-----------|------|
| `create-collectible` | `/create-item` | 创建被动道具 |
| `create-familiar` | `/create-familiar` | 创建跟班实体 |
| `add-callback` | `/add-callback` | 添加 Mod 回调 |
| `add-boss` | `/add-boss` | 添加 Boss |
| `add-card` | `/add-card` | 添加卡牌 |
| `add-pill` | `/add-pill` | 添加药丸 |
| `add-trinket` | `/add-trinket` | 添加饰品 |
| `add-save-data` | `/add-save-data` | 添加存档数据 |
| `validate-project` | `/validate` | 验证项目 XML/Lua |
| `debug-from-log` | `/debug` | 从 log 调试 |

## 不变量

1. `AgentSession` 为 **Transient** 生命周期；每个聊天 Tab 独立会话。
2. 历史裁剪：`MaxHistoryMessages = 50` 条 **或** `MaxContextChars = 120_000` 字符，先到先裁。
3. 裁剪时保护 `tool_calls` / `tool_result` 配对，不留下孤儿消息。
4. 工具结果经 `SanitizeToolResult` 加边界标记，防 prompt 注入。
5. Skill 激活后注入 prompt 片段与预取消息，**不替代** Tool Calling。
6. **Checkpoint** 状态（含 Before-image）仅属于该 live `AgentSession`；不跨重启、不跨已关闭 Tab 持久化。
7. `TrimHistory` 丢弃游标落在被裁区间（或因此失效）的 Checkpoint；保留游标仍落在保留历史上的 Checkpoint。

## Checkpoint 合同（产品 / 设计；实现按模块 PR）

术语见根目录 `CONTEXT.md`（Checkpoint、Restore、Before-image、Tracked write、Hand-edit、Hand-edit conflict mode）。地图：[Checkpoint contract (VS Code Chat–style restore)](https://github.com/Skymly/IsaacAgent/issues/19)。

### 生命周期

- 在 live 聊天 Tab / `AgentSession` 处理**每条用户消息之前**自动创建 Checkpoint。
- **Restore**：截断该用户回合及之后的对话；按 Before-image 回滚 Tracked write；若有进行中生成则先取消；将该条用户提示词交还 App 回填输入框（UX 见 [App.md](App.md)）。
- 无 Redo；无 Edit-previous；无跨重启 Checkpoint。

### Tracked write 与捕获缝

| 合同类 | 工具 |
|--------|------|
| **Tracked write**（须可经 Before-image Restore） | `write_file`、`diff_apply`、`batch_edit`、`scaffold_mod` |
| **Untracked**（不保证可恢复） | `run_command` |

- **懒捕获**：某 Checkpoint 之后，路径**首次**被 Tracked write 变更前捕获 Before-image。
- **首选缝**：`ToolRegistry.ExecuteAsync`——在调用 `tool.ExecuteAsync` 之前，用 `toolName` + JSON 参数（+ `CurrentProjectDir` / `scaffold_mod` 固定输出名）推导路径集并 `MaybeCaptureBeforeImage`。不依赖 `OnToolCall`（无法阻断写入）。
- **`scaffold_mod`**：按**路径**各自捕获 / 回滚，非整次调用原子集（与 `batch_edit`「无事务」一致）。
- 研究清单：[checkpoint-tracked-write-hooks.md](../research/checkpoint-tracked-write-hooks.md)。

### Before-image 限额

- 路径须在项目沙箱内（与 Tracked write 相同的 Core [`ProjectPathSafety`](Core.md) / `Resolve`；scaffold 固定名在 `projectDir` 下）。Before-image 字典键由 Agent 内 `CheckpointRelativePaths.ToRelativeKey` 规范化为 `/` 相对路径（非 Core API）。
- 仅 **UTF-8 文本**；单文件内容 ≤ **256 KB**（与 App 拖放注入聊天上限对齐）。
- 二进制、超限、越界 → **不捕获**；Restore 时按「缺可用 Before-image」处理。

### Restore（Agent 侧语义）

- 应用有可用 Before-image 的路径；**缺图**或按 Hand-edit conflict mode **skip** 的路径：保留磁盘现状，列入结果清单，**仍完成**对话截断。
- **Hand-edit**：以「上次成功 Tracked write 留下的 tip」哈希（非 Before-image）与盘上内容比较；删除 / 不可读视为冲突。
- **Hand-edit conflict mode**：`force`（默认）始终应用 Before-image；`skip` 跳过冲突路径并列出。配置面在 App Settings。
- **多 Tab 同 `projectDir`**：各 `AgentSession` 隔离；不跨 Tab 共享 Before-image；重叠 Restore = 磁盘 last-writer-wins（无跨 Tab 事务）。

### 历史裁剪 vs 游标

见不变量第 7 条。

### 可观测性

结构化 `ILogger` 生命周期日志（创建 Checkpoint、Before-image 捕获/跳过及原因、Restore 开始/完成、skip 路径摘要）。合同**不要求**指标 / 遥测产品。

### 实现 PR 顺序

**Agent → App → Tools（仅当需要）**。捕获缝在 Agent；工具实现体可不改。见 [Sequence Checkpoint implementation PRs by module](https://github.com/Skymly/IsaacAgent/issues/31)。

## 实现概览

### 关键类

| 类 | 职责 |
|----|------|
| `AgentSession` | 主循环：`StreamAsync` → tool call 累加 → `ExecuteAsync` → 继续对话；Checkpoint 生命周期宿主（自动创建 + trim 丢弃 + 懒 Before-image + **Restore**） |
| `Checkpoint` | 会话内对话锚点（`Id` + `UserMessage` 引用游标 + `BeforeImages`）；暴露于 `AgentSession.Checkpoints` |
| `BeforeImage` | 路径首次 Tracked write 前的内容或 create tombstone |
| `BeforeImageCapturer` | 懒捕获；路径沙箱检查走 Core `ProjectPathSafety.Resolve` |
| `CheckpointRelativePaths` | Checkpoint 相对路径键规范化（`ToRelativeKey`）；沙箱策略不在此 |
| `CheckpointRestorer` | Restore：按 Before-image 回滚；Hand-edit 用 tip 哈希比较；`force`/`skip` |
| `TrackedWriteTipStore` | 会话内「上次成功 Tracked write」tip 哈希（仅哈希，不存正文） |
| `ToolRegistry` | 注册 16 个 `ITool`；`Reconfigure(projectDir)` 更新项目上下文；Tracked write 前捕获 Before-image，成功后记录 tip |
| `SkillRegistry` | 注册 10 个 `ISkill`；`ResolveActiveSkills(userMessage)` |
| `SystemPrompts` | 基础 prompt + 工具列表 + Guidelines |

### 请求流程

```
用户消息
  → SkillRegistry 解析激活 Skill
  → Skill.PreFetchContextAsync（可选 RAG）
  → 创建 Checkpoint（锚到即将入史的 user ChatMessage）+ 结构化日志
  → 组装 messages + tools schema
  → LLM StreamAsync
  → 累加 tool_calls（按 index 分桶）
  → ToolRegistry.ExecuteAsync
       Tracked write → 懒 Before-image（`BeforeImageCapturer`）→ tool.ExecuteAsync → 成功则记录 tip 哈希
  → SanitizeToolResult → 追加 tool 消息
  → 循环或结束

Restore（`AgentSession.RestoreAsync`）：
  → 结构化日志 Restore started
  → 截断 Checkpoint 用户回合及之后历史（含该回合前紧邻的 Skill pre-fetch system 消息）；丢弃该 Checkpoint 及之后的 Checkpoint
  → 对缺图路径列入 `missing-before-image`；对有 Before-image 的路径：
       `skip` 模式：tip 哈希与盘上比较；删除 / 分歧 → `hand-edit`；不可读或缺 tip → `unreadable`
       `force`（默认）：始终应用 Before-image（tombstone → 删除；内容 → 写回）
  → 清空 tip 存储（含提前退出路径）；日志 Restore completed + skip 摘要
  → 返回 `RestoreResult`（含 `UserPrompt` 供 App 回填）
```

Checkpoint 游标是 `ChatMessage` **引用**（非易变下标）。`ClearHistory` / `LoadHistory` / `Dispose` 清空 Checkpoint 列表与 tip 存储（不跨重启持久化）。懒 Before-image：某路径在 Checkpoint 之后**首次**被 Tracked write 触碰时捕获；同路径再次写入不替换；二进制 / &gt;256KB / 越界跳过并打日志；`run_command` 不捕获。Restore 不回滚 `run_command` 副作用。路径沙箱权威实现：Core [`ProjectPathSafety`](Core.md)；Agent 不再保留 `Resolve` / `IsWithinProject` 副本（`ToRelativeKey` 仍属 Checkpoint，见 `CheckpointRelativePaths`）。

### 历史裁剪

`TrimHistory` 先按条数后按字符；删除 assistant+tool 组时保持 tool_call_id 配对完整；裁剪后丢弃 `UserMessage` 不再位于保留历史中的 Checkpoint，保留游标仍有效的 Checkpoint。

## 设计权衡

- **Skill vs 更多工具**：Skill 只做 prompt/RAG 增强，复杂操作仍走 Tool Calling（见 ADR-004）。
- **Transient Session**：避免 Tab 间状态泄漏；代价是每次新建需 reconfigure tools；亦支撑 Checkpoint 按会话隔离。
- **懒 Before-image vs 回合开始全量快照**：只为实际被写路径付成本；与 VS Code Chat 式「仅恢复 agent 写入」对齐。
- **不恢复 `run_command`**：任意 shell 副作用无法可靠建模；确认框必须明示。

## 兼容基线

- .NET 8
- OpenAI-compatible / Ollama 流式 API（见 [LLM.md](LLM.md)）

## 不在范围内

- UI 聊天渲染与 Settings 控件（见 [App.md](App.md)）
- 具体工具参数 schema（见 [Tools.md](Tools.md)）
- Redo、Edit-previous、跨重启 Checkpoint、Git / 全工作区 rewind
- 保证 `run_command`（或其他 untracked）副作用可恢复

## 已知局限

- 单 chunk 多 tool call 依赖 provider 正确 yield
- 无多 Agent 协作或子 Agent 委派
- 多 Tab 同目录重叠 Restore 无协调（last-writer-wins）
- Checkpoint **自动创建 + trim 丢弃**、**懒 Before-image 捕获**、**Restore**（含 Hand-edit `force`/`skip`）已落地于 `AgentSession`；App UX（每消息 Restore 控件 / Settings）另见 App 模块票

## 参考

- `src/IsaacAgent.Agent/Engine/AgentSession.cs`
- `src/IsaacAgent.Agent/Engine/ToolRegistry.cs`
- `src/IsaacAgent.Agent/Skills/`
- `CONTEXT.md`
- [docs/research/checkpoint-tracked-write-hooks.md](../research/checkpoint-tracked-write-hooks.md)

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

## 实现概览

### 关键类

| 类 | 职责 |
|----|------|
| `AgentSession` | 主循环：`StreamAsync` → tool call 累加 → `ExecuteAsync` → 继续对话 |
| `ToolRegistry` | 注册 16 个 `ITool`；`Reconfigure(projectDir)` 更新项目上下文 |
| `SkillRegistry` | 注册 10 个 `ISkill`；`ResolveActiveSkills(userMessage)` |
| `SystemPrompts` | 基础 prompt + 工具列表 + Guidelines |

### 请求流程

```
用户消息
  → SkillRegistry 解析激活 Skill
  → Skill.PreFetchContextAsync（可选 RAG）
  → 组装 messages + tools schema
  → LLM StreamAsync
  → 累加 tool_calls（按 index 分桶）
  → ToolRegistry.ExecuteAsync
  → SanitizeToolResult → 追加 tool 消息
  → 循环或结束
```

### 历史裁剪

`TrimHistory` 先按条数后按字符；删除 assistant+tool 组时保持 tool_call_id 配对完整。

## 设计权衡

- **Skill vs 更多工具**：Skill 只做 prompt/RAG 增强，复杂操作仍走 Tool Calling（见 ADR-004）。
- **Transient Session**：避免 Tab 间状态泄漏；代价是每次新建需 reconfigure tools。

## 兼容基线

- .NET 8
- OpenAI-compatible / Ollama 流式 API（见 LLM Design Doc，待补）

## 不在范围内

- UI 聊天渲染（见 [App.md](App.md)）
- 具体工具参数 schema（见 [Tools.md](Tools.md)）

## 已知局限

- 单 chunk 多 tool call 依赖 provider 正确 yield
- 无多 Agent 协作或子 Agent 委派

## 参考

- `src/IsaacAgent.Agent/Engine/AgentSession.cs`
- `src/IsaacAgent.Agent/Skills/`

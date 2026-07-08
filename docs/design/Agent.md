# Design Doc: Agent

> **关联 Spec**：[docs/spec/Agent.md](../spec/Agent.md)
> **关联 ADR**：[ADR-004](../adr/ADR-004-skill-layer-above-tools.md)

## 概述

`IsaacAgent.Agent` 实现 LLM 会话编排：System Prompt 组装、Skill 激活、Tool Calling 循环、流式输出与历史裁剪。

## 设计目标

- 将 Isaac 领域知识通过 Skill + RAG 预取注入，减少用户提示工程
- 保持原子工具可组合、可测试
- 单 Tab 单会话，切换项目时正确 Dispose

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

## 已知局限

- 单 chunk 多 tool call 依赖 provider 正确 yield（见 LLM provider 修复记录）
- 无多 Agent 协作或子 Agent 委派

## 参考

- `src/IsaacAgent.Agent/Engine/AgentSession.cs`
- `src/IsaacAgent.Agent/Skills/`

# Spec: Agent

> **版本**：v0.2.x（与 Git tag 对齐）
> **关联 Design Doc**：[docs/design/Agent.md](../design/Agent.md)
> **关联 ADR**：[ADR-001](../adr/ADR-001-multi-project-layered-architecture.md)、[ADR-004](../adr/ADR-004-skill-layer-above-tools.md)

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

### 会话不变量

1. `AgentSession` 为 **Transient** 生命周期；每个聊天 Tab 独立会话。
2. 历史裁剪：`MaxHistoryMessages = 50` 条 **或** `MaxContextChars = 120_000` 字符，先到先裁。
3. 裁剪时保护 `tool_calls` / `tool_result` 配对，不留下孤儿消息。
4. 工具结果经 `SanitizeToolResult` 加边界标记，防 prompt 注入。
5. Skill 激活后注入 prompt 片段与预取消息，**不替代** Tool Calling。

## 兼容基线

- .NET 8
- OpenAI-compatible / Ollama 流式 API（见 `IsaacAgent.LLM` Spec，待补）

## 不在范围内

- UI 聊天渲染（见 `design/App.md`）
- 具体工具参数 schema（见 [spec/Tools.md](Tools.md)）

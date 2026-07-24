# Design Doc: LLM

> **关联 ADR**：[ADR-001](../adr/ADR-001-multi-project-layered-architecture.md)
> **关联 Roadmap**：R-010

## 概述

`IsaacAgent.LLM` 提供聊天补全的 OpenAI 兼容 / Ollama 适配层：实现 Core 中的 `IChatService`，支持流式输出、tool calling，以及可热替换的 DI 代理。契约类型（`ChatRequest` / `ChatMessage` / `ChatChunk` 等）住在 `IsaacAgent.Core`；本模块只做传输与协议适配。

## 设计目标

- 统一两种常见本地/远程端点，供 `AgentSession` 无差别调用
- 流式优先：UI 与 tool-call 累加依赖 `StreamAsync`
- Settings apply 可立即换 chat provider，无需重启
- 对瞬态网络故障有限重试；对畸形流行与空闲挂起有明确失败语义

## API 面

### 契约（`IsaacAgent.Core`）

| 类型 | 职责 |
|------|------|
| `IChatService` | `StreamAsync` / `CompleteAsync` |
| `ChatRequest` | messages、tools、model、temperature、max_tokens |
| `ChatResponse` | 完整 assistant 消息 + token 用量 |
| `ChatChunk` | 文本 delta，或带 index/id/name/arguments 的 tool-call 片段 |
| `ChatMessage` / `ToolCall` / `ToolDefinition` | 会话与工具 schema |

### 本模块类型（`IsaacAgent.LLM`）

| 类型 | 职责 |
|------|------|
| `ProviderType` | `OpenAICompatible` \| `Ollama` |
| `ProviderConfig` | Type、Endpoint、Model、ApiKey、TimeoutSeconds |
| `LlmServiceRegistration.AddLlmProvider` | 注册 `ChatServiceProxy` 为单例 `IChatService` |
| `LlmServiceRegistration.BuildProvider` | 按 config 构造 `HttpClient` + 具体 provider + `RetryChatService` |
| `ChatServiceProxy` | 热替换内层 `IChatService`；`Replace` 原子交换并 Dispose 旧实例 |
| `RetryChatService` | 装饰器：仅瞬态错误重试 |
| `OpenAICompatibleProvider` | `POST /v1/chat/completions`（SSE） |
| `OllamaProvider` | `POST /api/chat`（NDJSON） |

### 配置入口（App）

| 来源 | 说明 |
|------|------|
| `AppConfiguration` | 持久化 ProviderType / Endpoint / Model / DPAPI 加密 ApiKey |
| `ProviderIntent.Chat` | Settings apply 消费的内存快照（非再次 Load） |
| `SettingsApply` | `_chatProxy.Replace(_buildChat(intent.Chat))` 立即换源 |

嵌入（ONNX / Ollama embedding）**不属于**本模块；见 [Rag.md](Rag.md)。

## 不变量

1. DI 对外只暴露一个 `IChatService`：始终是 `ChatServiceProxy`；内层为 `RetryChatService(concrete)`。
2. `ChatServiceProxy.Replace` 用 `Interlocked.Exchange`；旧实例若实现 `IDisposable` 必须 Dispose（释放 `HttpClient`）。
3. Payload 用 `Dictionary` 构建：无 tool calls 时**省略** `tools`；消息级仅在有值时发送 `tool_calls` / `tool_call_id`（避免敏感端点因 `null` 字段失败）。
4. 流式 delta 中单个 chunk 可含多个 `tool_calls`：每个元素各 yield 一个 `ChatChunk`，供 `AgentSession` 按 index 分桶。
5. `RetryChatService` 仅对 `HttpRequestException` / `TimeoutException` / `IOException` 重试；用户取消立即传播。流式在**已 yield 过任意 chunk 后**不再重试（避免重复半段输出）。
6. 流读取空闲超时默认 **90s**（`StreamReadTimeout`）；超时抛 `TimeoutException`（可被 Retry 视为瞬态，若尚未 yield）。
7. HTTP 429 / 401 / 403 转成带说明的 `HttpRequestException`；429 可进入重试。

## 实现概览

### 注册与热替换

```
AddLlmProvider(config)
  → ChatServiceProxy(BuildProvider(config)) as IChatService

BuildProvider(config)
  → HttpClient(BaseAddress, Timeout, optional Bearer)
  → OpenAICompatibleProvider | OllamaProvider
  → RetryChatService(inner, maxRetries=3, delays=[2s,5s,10s])

Settings apply
  → BuildProvider(providerIntent.Chat)
  → ChatServiceProxy.Replace(...)
```

### Provider 差异

| | OpenAICompatible | Ollama |
|--|------------------|--------|
| 路径 | `/v1/chat/completions` | `/api/chat` |
| 流格式 | SSE `data: …` / `[DONE]` | NDJSON 行 |
| Token 字段 | `usage.prompt_tokens` / `completion_tokens` | `prompt_eval_count` / `eval_count` |
| Tool schema | OpenAI `tools[].function` | Ollama `tools`（同构 function 形态） |

畸形 JSON 行：log warning 并 skip，不中断整条流。

### 与 Agent 的边界

- `AgentSession` 只依赖 `IChatService.StreamAsync`；本模块不编排 tool 循环。
- Tool schema 由 Agent / Tools 组装进 `ChatRequest.Tools`；LLM 只序列化与回传片段。

## 设计权衡

- **契约在 Core、实现在 LLM**：符合 ADR-001 分层；Agent / App 不引用具体 HTTP 细节。
- **Proxy + 工厂 vs 重建整个 DI 图**：Settings apply 只换 chat 内层，会话与 UI 订阅保持稳定。
- **有限重试 vs 无限**：3 次指数式退避覆盖瞬时 429/断网；认证失败不空转。
- **空闲超时独立于 HttpClient.Timeout**：headers 已返回后挂起的 body 读，单靠 HttpClient 超时不可靠。

## 不在范围内

- 多套 LLM Profile 预设（Roadmap R-020）
- 嵌入 provider（Rag）
- Provider 插件动态加载
- 非 OpenAI 兼容 / 非 Ollama 协议（Anthropic Messages API 等）

## 已知局限

- `CompleteAsync` 用量字段依赖端点返回完整 `usage`；部分兼容实现可能缺字段导致解析失败
- 重试不覆盖「已开始流式输出后」的中途断开
- 单活跃 chat provider；无按 Tab / 按请求覆盖 endpoint
- ApiKey 仅经 `HttpClient` Bearer 头；不支持 query-string 或自定义 header 方案

## 参考

- `src/IsaacAgent.LLM/`
- `src/IsaacAgent.Core/Services/IChatService.cs`
- `src/IsaacAgent.Core/Models/ChatMessage.cs`
- `docs/design/App.md`（Settings apply / provider intent）
- `docs/design/Agent.md`（流式 tool-call 累加）
- `CONTEXT.md`（Settings apply / provider intent 术语）

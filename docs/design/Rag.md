# Design Doc: Rag

> **关联 ADR**：[ADR-002](../adr/ADR-002-local-rag-embedded-knowledge.md)

## 概述

`IsaacAgent.Rag` 在本地构建向量索引，为 Agent 提供语义检索与 XML 校验能力。

## 设计目标

- 离线可用，无外部向量服务
- 嵌入资源随程序发布，与 Isaac API 版本对齐
- 设置页可热切换嵌入 provider 并后台重建索引

## API 面

### 核心接口

| 接口 | 职责 |
|------|------|
| `IRetriever` | 向量检索入口 |
| `IEmbeddingProvider` | 文本 → 向量（ONNX 或 Ollama） |
| `IVectorStore` | 内存向量存储与余弦相似度搜索 |

### 知识库资源

嵌入于 `IsaacAgent.Rag/Resources/`：

- 480+ Markdown API 文档（vanilla + Repentogon）
- 25 Mod patterns
- 35 官方 XSD

### 配置（`AppConfiguration`）

| 字段 | 说明 |
|------|------|
| `EmbeddingProvider` | `onnx`（默认）或 `ollama` |
| `OllamaBaseUrl` / `OllamaEmbeddingModel` | Ollama 嵌入端点 |
| `OnnxModelPath` | 本地 ONNX 模型路径（可选） |

## 不变量

1. 索引在内存 `InMemoryVectorStore`；启动或设置变更时后台重建。
2. `InMemoryVectorStore.Search` 使用防御性快照（`ToList()`），避免并发修改。
3. ONNX 嵌入维度从模型输出推断，不硬编码。
4. `search_knowledge` / `get_pattern` 依赖已构建索引；索引未就绪时返回明确错误。
5. 嵌入资源文档为**产品知识**，与维护者 `docs/` 体系分离。

## 实现概览

### 索引管线

```
Resources/docs/**/*.md
  → ApiDocChunker / PatternChunker（MkDocs 风格分块）
  → IndexBuilder（批量嵌入，O(batch) GetRange）
  → InMemoryVectorStore.ReplaceAll
```

### 嵌入 Provider

| Provider | 实现 | 备注 |
|----------|------|------|
| ONNX | `OnnxEmbeddingProvider` | 默认；`_sessionLock` 保护 InferenceSession |
| Ollama | `OllamaEmbeddingProvider` | 远程嵌入 API |

`EmbeddingProviderProxy` 支持热替换并 Dispose 旧 session。

### 检索

`InMemoryVectorStore.Search`：余弦相似度 Top-K；搜索时快照防并发。

### App 集成

- 启动时 `PrewarmRagIndexAsync` 后台构建
- 失败时 `SettingsViewModel.SetIndexStatus` 显示错误
- Settings 保存触发 `ReloadEmbeddingProvider` + 重建

## 设计权衡

- **内存索引 vs 持久化**：简化部署；代价是每次启动重建、内存占用。
- **嵌入资源 vs 在线拉取**：保证离线；更新知识需发版或后续支持用户扩展目录。

## 兼容基线

- .NET 8
- Windows（App 层索引 UI）；库层可在 CI `CiLib` 跨平台测试

## 不在范围内

- MkDocs 站点发布
- 外部向量数据库（Pinecone 等）

## 已知局限

- 大知识库首次索引耗时数秒至数十秒
- 无增量索引；全量 ReplaceAll

## 参考

- `src/IsaacAgent.Rag/Indexing/`
- `src/IsaacAgent.Rag/Resources/`

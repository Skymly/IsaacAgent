# Design Doc: Rag

> **关联 Spec**：[docs/spec/Rag.md](../spec/Rag.md)
> **关联 ADR**：[ADR-002](../adr/ADR-002-local-rag-embedded-knowledge.md)

## 概述

`IsaacAgent.Rag` 在本地构建向量索引，为 Agent 提供语义检索与 XML 校验能力。

## 设计目标

- 离线可用，无外部向量服务
- 嵌入资源随程序发布，与 Isaac API 版本对齐
- 设置页可热切换嵌入 provider 并后台重建索引

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

## 已知局限

- 大知识库首次索引耗时数秒至数十秒
- 无增量索引；全量 ReplaceAll

## 参考

- `src/IsaacAgent.Rag/Indexing/`
- `src/IsaacAgent.Rag/Resources/`

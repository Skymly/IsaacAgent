# Design Doc: Rag

> **关联 ADR**：[ADR-002](../adr/ADR-002-local-rag-embedded-knowledge.md)

## 概述

`IsaacAgent.Rag` 在本地构建向量索引，为 Agent 提供语义检索与 XML 校验能力。

## 设计目标

- 离线可用，无外部向量服务
- 嵌入资源随程序发布，与 Isaac API 版本对齐
- 设置页可热切换嵌入 provider 并后台重建索引
- **ONNX 默认零配置**：随包分发 all-MiniLM-L6-v2（`model.onnx` + `vocab.txt`）

## API 面

### 核心接口

| 接口 | 职责 |
|------|------|
| `IRetriever` | 向量检索入口 |
| `IEmbeddingProvider` | 文本 → 向量（ONNX 或 Ollama） |
| `IVectorStore` | 内存向量存储与余弦相似度搜索 |
| `EmbeddingApply` | **Embedding apply**：换嵌入 provider（允许维度变化）→ 作废知识索引 → 重建；新一次 apply / 外部取消令牌可取消进行中的重建 |

### 知识库资源

嵌入于 `IsaacAgent.Rag/Resources/`：

- 480+ Markdown API 文档（vanilla + Repentogon）
- 25 Mod patterns
- 35 官方 XSD

### 配置（`AppConfiguration` / `EmbeddingConfig`）

| 字段 | 说明 |
|------|------|
| `EmbeddingSource` | **`Onnx`（默认）** 或 `Ollama` |
| `OllamaEmbeddingEndpoint` / `OllamaEmbeddingModel` | Ollama 嵌入端点 |
| `OnnxEmbeddingModelPath` / `OnnxEmbeddingVocabPath` | 可选覆盖；**留空则使用捆绑资源** `onnx/model.onnx` 与 `onnx/vocab.txt`（相对 `AppContext.BaseDirectory`） |

路径解析由 `DefaultOnnxAssets.ResolveModelPath` / `ResolveVocabPath` 完成。

## 不变量

1. 查询期使用内存 `InMemoryVectorStore`；磁盘缓存为 `%APPDATA%\IsaacAgent\rag\index.bin`（启动时优先加载，模型名/维度不匹配时全量重建）。
2. `InMemoryVectorStore.Search` 使用防御性快照（`ToList()`），避免并发修改。
3. ONNX 嵌入维度从模型输出推断，不硬编码。
4. `search_knowledge` / `get_pattern` 依赖已构建索引；索引未就绪时返回明确错误。
5. 嵌入资源文档为**产品知识**，与维护者 `docs/` 体系分离。
6. 默认 ONNX 路径为空时必须解析到捆绑资产；不得要求用户手动下载模型才能首次使用。

## 实现概览

### 索引管线

```
Resources/docs/**/*.md
  → ApiDocChunker / PatternChunker（MkDocs 风格分块）
  → IndexBuilder（批量嵌入，O(batch) GetRange）
  → InMemoryVectorStore.ReplaceAll
  → SaveAsync(index.bin)
```

### 嵌入 Provider

| Provider | 实现 | 备注 |
|----------|------|------|
| ONNX | `OnnxEmbeddingProvider` | **默认**；捆绑 all-MiniLM-L6-v2；`_sessionLock` 保护 InferenceSession |
| Ollama | `OllamaEmbeddingProvider` | 可选远程嵌入 API |

构建：`IsaacAgent.Rag.csproj` 的 `EnsureOnnxAssets` 在缺少 `Resources/onnx/model.onnx` 时从 Hugging Face 下载；`vocab.txt` 入库。资产同时作为 Content（旁路 `onnx/`）与 EmbeddedResource 打包；单文件发布时由 `DefaultOnnxAssets` 解压到 `%APPDATA%\IsaacAgent\onnx\`。

`EmbeddingProviderProxy` 支持热替换并 Dispose 旧 session。维度变化时须走 **Embedding apply**（`EmbeddingApply.ApplyAsync`）：先 `ResetReady` + 清空内存索引 + 删除磁盘缓存，再 `Replace`，再 `RebuildIndexAsync`。裸 `Replace` 不再拦截维度不匹配。

**取消**：`ApplyAsync` 接受 `CancellationToken`（应用关闭可传入 shutdown token）。新一次 Embedding apply 会取消仍在进行的重建，使最终索引与最新 provider 意图一致；被取消的重建不得将知识索引标为 ready，也不应表现为成功完成。取消后再次 Embedding apply 可正常完成重建。

### 检索

`InMemoryVectorStore.Search`：余弦相似度 Top-K；搜索时快照防并发。

### App 集成

- 启动时 `PrewarmRagIndexAsync` 后台确保索引（加载缓存或重建）
- 失败时 `SettingsViewModel.SetIndexStatus` 显示错误
- Settings 保存将改为 **Settings apply**（见 CONTEXT.md）；现阶段仍可能经 `ReloadEmbeddingProvider` 手搓，目标路径为 `EmbeddingApply`

## 设计权衡

- **内存查询 + 磁盘缓存**：冷启动可跳过重建；模型/维度变化仍全量 ReplaceAll。
- **捆绑 ONNX vs 在线拉取**：默认离线可用；约 90 MB 模型不入库，由构建目标下载并经 CI cache 加速。
- **嵌入资源知识 vs 用户扩展**：产品知识随发版；用户目录追加知识见 Roadmap R-012。

## 兼容基线

- .NET 8
- Windows x64（严格 Windows-only；见 [ADR-003](../adr/ADR-003-windows-only-avalonia-desktop.md)）

## 不在范围内

- MkDocs 站点发布
- 外部向量数据库（Pinecone 等）

## 已知局限

- 大知识库首次索引（无可用 `index.bin` 时）耗时数秒至数十秒
- 无增量索引；全量 ReplaceAll
- 自包含发布体积因捆绑 ONNX 模型明显增大

## 参考

- `src/IsaacAgent.Rag/Indexing/`
- `src/IsaacAgent.Rag/Embedding/DefaultOnnxAssets.cs`
- `src/IsaacAgent.Rag/Resources/onnx/`

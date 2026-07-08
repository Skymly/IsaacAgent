# Spec: Rag

> **版本**：v0.2.x（与 Git tag 对齐）
> **关联 Design Doc**：[docs/design/Rag.md](../design/Rag.md)
> **关联 ADR**：[ADR-002](../adr/ADR-002-local-rag-embedded-knowledge.md)

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

## 兼容基线

- .NET 8
- Windows（App 层索引 UI）；库层可在 CI `CiLib` 跨平台测试

## 不在范围内

- MkDocs 站点发布
- 外部向量数据库（Pinecone 等）

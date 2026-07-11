# ADR-002: Local RAG with embedded knowledge base

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-01 |
| **关联 Issue** | 无 — 项目初始架构 |

## 背景

Agent 需要 Isaac API、Mod 模式与官方 XSD 的语义检索能力；用户可能离线或不愿依赖外部向量服务。

## 决策

- 知识库以 **嵌入资源** 形式随 `IsaacAgent.Rag` 发布（480+ Markdown、25 patterns、35 XSD）。
- 索引在本地构建：MkDocs chunker + **ONNX**（默认）或 **Ollama** 嵌入。
- 向量存储使用 **内存** `InMemoryVectorStore`（启动时/设置变更时重建索引）。

## 后果

- 正面：无外部向量 DB 依赖；隐私友好；与 Isaac 垂直领域强绑定。
- 负面：首次索引耗时；大知识库内存占用；索引重建需 UI 反馈（Settings 进度）。

## 参考

- [design/Rag.md](../design/Rag.md)

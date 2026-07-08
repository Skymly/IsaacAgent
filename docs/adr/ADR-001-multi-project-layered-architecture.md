# ADR-001: Multi-project layered architecture

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-01 |
| **关联 RFC** | 无 — 项目初始架构 |

## 背景

IsaacAgent 需要同时承载桌面 UI、LLM 调用、RAG 检索、Agent 编排与 Isaac 专用工具，且希望核心逻辑可独立测试、不绑定 Avalonia。

## 决策

采用六项目分层：

- `IsaacAgent.App` — Avalonia UI（MVVM）
- `IsaacAgent.Agent` — 会话编排、Skill、Tool 路由
- `IsaacAgent.Tools` / `IsaacAgent.Rag` — 原子工具与 RAG 管线
- `IsaacAgent.LLM` — LLM 提供商抽象
- `IsaacAgent.Core` — 领域模型、接口、内置 API 知识

依赖方向：App → Agent → (Tools, Rag, LLM) → Core。App 不直接被 Tools/Rag 引用以外的下层引用。

## 后果

- 正面：模块边界清晰；库层测试无需 UI；可单独演进 RAG/LLM。
- 负面：跨模块变更需多项目 PR 或严格按模块拆分 Issue/PR。

## 参考

- [AGENTS.md](../../AGENTS.md) 项目结构

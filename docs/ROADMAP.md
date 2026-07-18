# Roadmap

> 滚动维护的功能与技术 backlog。技术债明细见 [ISSUES.md](../ISSUES.md)。
> 状态：`候选` → `排期` → `进行中` → `已完成（归档）` / `暂缓` / `明确不做`

最后更新：2026-07-11

---

## 进行中

（无）

---

## 排期

| ID | 项 | 说明 | 目标阶段 |
|----|-----|------|----------|
| R-010 | LLM Design Doc | 为 `IsaacAgent.LLM` 补充 Design Doc | v0.3 |
| R-011 | E2E 测试扩展 | 扩展 `IsaacAgent.E2ETest` 覆盖关键用户流程 | v0.3 |
| R-012 | 用户可扩展 RAG 知识 | 支持用户目录追加知识块 | v0.4 |

---

## 候选

| ID | 项 | 说明 |
|----|-----|------|
| R-020 | 多 LLM 配置 Profile | 保存多套 endpoint / model 预设 |
| R-021 | Skill 可视化编辑 | 用户自定义 Skill prompt 片段 |
| R-022 | 对话导出 | 导出 chat 为 Markdown / JSON |
| R-023 | 插件化工具 | 第三方 `ITool` 动态加载 |

---

## 暂缓

（无）

---

## 明确不做

| ID | 项 | 理由 |
|----|-----|------|
| R-030 | macOS / Linux 桌面构建与跨平台库 CI | 严格 Windows-only（[ADR-003](adr/ADR-003-windows-only-avalonia-desktop.md)）；不维护 `CiLib` / `ci-lib` |
| R-040 | 云端向量数据库 | 与本地 RAG 定位冲突（[ADR-002](adr/ADR-002-local-rag-embedded-knowledge.md)） |

---

## 已完成（归档）

| ID | 项 | 完成版本 | 备注 |
|----|-----|----------|------|
| R-001 | 文档体系落地并精简 | — | 保留 ADR + Design Doc + Roadmap；RFC/Spec/Plan/Review 已移除 |
| R-100 | Avalonia 测试稳定性 | v0.2.4 | [ADR-005](adr/ADR-005-headless-unit-test-session.md) |
| R-101 | MinVer 版本管理 | v0.2.0 | Git tag 驱动 |
| R-102 | 16 Agent 工具 + 10 Skill | v0.2.x | [design/Tools.md](design/Tools.md)、[design/Agent.md](design/Agent.md) |
| R-103 | 本地 RAG 嵌入知识库 | v0.1.x | [ADR-002](adr/ADR-002-local-rag-embedded-knowledge.md) |

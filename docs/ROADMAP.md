# Roadmap

> 滚动维护的功能与技术 backlog。技术债明细见 [ISSUES.md](../ISSUES.md)。
> 状态：`候选` → `排期` → `进行中` → `已完成（归档）` / `暂缓` / `明确不做`

最后更新：2026-08-08

**v0.4 主题**：User knowledge（R-012）— Rag 索引 + App Settings 已落地。

---

## 进行中

（无）

---

## 排期

（无）

---

## 候选

| ID | 项 | 说明 |
|----|-----|------|
| R-020 | 多 LLM 配置 Profile | 保存多套 endpoint / model 预设 |
| R-021 | Skill 可视化编辑 | 用户自定义 Skill prompt 片段 |
| R-023 | 插件化工具 | 第三方 `ITool` 动态加载（靠后；与编译期 `ToolRegistry` 冲突大） |

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
| R-012 | 用户可扩展 RAG 知识 | — | User knowledge：`%APPDATA%\IsaacAgent\knowledge`；遗留 `rag/examples` 搬家；Settings 打开/重建；见 [design/Rag.md](design/Rag.md)、[design/App.md](design/App.md) |
| R-105 | 立刻打 `v0.3` tag | v0.3.0 | Git tag `v0.3.0` + GitHub Release；CHANGELOG 收束 Unreleased |
| R-011 | 非 UI Agent/工具链 E2E | v0.3.0 | Spec #94；ticket #95；`AgentToolChainIntegrationTests`（scripted LLM + production Tools via `ReconfigureForProject`；`read_file`/`write_file`/`list_files`/`validate_xml`）；Nuke `UnitTest`/`Ci`/`CiAll`；正交于 FlaUI；非 `tools/e2e-test` |
| R-014 | FlaUI 加深（Chat/Settings + Publish smoke） | — | Spec #86；tickets #87–#89；`UiTest` 全套（含 Chat/Settings）+ Nuke `UiTestPublish`（`Publish` + `FlaUI=PublishSmoke` A→B）；Nightly `ui-tests.yml` 同 job 两步；不进 PR `Ci`/`CiAll`/`Release` |
| R-013 | 桌面 UI 自动化（FlaUI Nightly A→B） | — | Spec #78；tickets #79–#81；`IsaacAgent.UiTests` + Nuke `UiTest` + `ui-tests.yml`；Publish 冒烟见 R-014 |
| R-022 | 对话导出（MVP） | — | 菜单导出 Markdown / JSON（live UI）；session-store 导出 / 保存对话框若需要则另开小票 |
| R-104 | 统一 Chat session store | — | Spec #46；tickets #47–#51；App `IChatSessionStore` / `sessions/`；见 [design/App.md](design/App.md)、`CONTEXT.md` |
| R-010 | LLM Design Doc | — | [design/LLM.md](design/LLM.md) |
| R-001 | 文档体系落地并精简 | — | 保留 ADR + Design Doc + Roadmap；RFC/Spec/Plan/Review 已移除 |
| R-100 | Avalonia 测试稳定性 | v0.2.4 | [ADR-005](adr/ADR-005-headless-unit-test-session.md) |
| R-101 | MinVer 版本管理 | v0.2.0 | Git tag 驱动 |
| R-102 | 16 Agent 工具 + 10 Skill | v0.2.x | [design/Tools.md](design/Tools.md)、[design/Agent.md](design/Agent.md) |
| R-103 | 本地 RAG 嵌入知识库 | v0.1.x | [ADR-002](adr/ADR-002-local-rag-embedded-knowledge.md) |

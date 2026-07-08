# 文档体系标准

> **权威源**。本文档定义 IsaacAgent 仓库**文档驱动开发（Documentation-Driven Development）**体系：所有文档的类型、结构、生命周期、归档规则，以及以文档为先导的开发流程。人类开发者和 AI 编码助手（Agent）均须遵守。`AGENTS.md`「文档体系」章节为本文档的精简摘要。
>
> - **核心原则**：**先文档后代码**——任何非琐碎变更，先确定它需要哪些文档、文档达到要求状态后才动代码（决策表见 [§11](#11-文档驱动开发流程)）。
> - **语言**：内部维护者文档以**中文**为主；Issue / commit / PR 以**英文**。
> - **冲突优先级**：`AGENTS.md` > `docs/DOCUMENTATION.md` > 其他文档。
> - **体系来源**：改编自 [Skymly/DesignPatterns](https://github.com/Skymly/DesignPatterns) 文档体系，按桌面 Agent 产品形态调整（子系统 Spec 替代模式 Spec，无诊断 ID）。

---

## 1. 文档类型总览

| 类型 | 目录 | 用途 | 稳定性 | 变更门槛 |
|------|------|------|--------|----------|
| **RFC** | `docs/rfc/` | 设计提案与讨论记录 | 提案阶段，频繁迭代 | 自由修改（Review 前） |
| **ADR** | `docs/adr/` | 架构决策记录（不可变卡片） | 已决策，仅追加 | 仅 Supersede，不修改原文 |
| **Spec** | `docs/spec/` | 稳定契约（API 面、工具 schema、不变量） | 版本化稳定 | 需 RFC + ADR 方可变更 |
| **Design Doc** | `docs/design/` | 实现细节、设计权衡、已知局限 | 随实现演进 | PR 随代码同步更新 |
| **Roadmap** | `docs/ROADMAP.md` | 功能与技术 backlog | 滚动维护 | 维护者评审 |
| **Plan** | `docs/plans/`（大型）/ GitHub Issue（小型） | 任务计划（目标、步骤、验收） | 短生命周期 | 计划内自由更新 |
| **Review** | `docs/review/` | 评审记录（设计 / 实现 / 阶段回顾） | Final 后不可变 | 仅勾选行动项与修复链接 |

### 1.1 不作为独立文档类型

| 内容 | 载体 |
|------|------|
| 编码规范、构建、模块边界 | `AGENTS.md`（权威源） |
| 开发环境、本地工作流 | `docs/DEVELOPMENT.md` |
| 变更日志 | `CHANGELOG.md`（Keep a Changelog 格式） |
| 贡献流程 | `CONTRIBUTING.md` |
| 技术债审计 | `ISSUES.md`（引用自 Roadmap，非文档体系正式类型） |
| 产品知识（Isaac API） | `src/IsaacAgent.Rag/Resources/`（嵌入 RAG，非维护者文档） |

### 1.2 子系统与 Spec 映射

| 项目 | Spec | Design Doc |
|------|------|------------|
| `IsaacAgent.Agent` | [spec/Agent.md](spec/Agent.md) | [design/Agent.md](design/Agent.md) |
| `IsaacAgent.Tools` + RAG 工具 | [spec/Tools.md](spec/Tools.md) | [design/Tools.md](design/Tools.md) |
| `IsaacAgent.Rag` | [spec/Rag.md](spec/Rag.md) | [design/Rag.md](design/Rag.md) |
| `IsaacAgent.App` | — | [design/App.md](design/App.md) |
| `IsaacAgent.LLM` | 待补 | 待补 |
| `IsaacAgent.Core` | 随各子系统 Spec 分散描述 | 随各 Design Doc |

---

## 2. RFC — Request for Comments

### 2.1 用途

对**有设计争议或影响面较大**的变更提出设计方案，供讨论与决策。小改动（bug fix、单方法新增）无需 RFC，直接 Issue + PR。

### 2.2 何时需要 RFC

| 场景 | 需要 RFC？ |
|------|-----------|
| 新增 Agent 工具（公共 schema 变更） | ✅ 必须 |
| 新增 Skill（新工作流契约） | ✅ 必须 |
| 新增或变更公共 API（破坏性） | ✅ 必须 |
| 跨模块架构变更 | ✅ 必须 |
| RAG 知识库结构重大变更 | ✅ 必须 |
| 单模块内 bug fix | ❌ Issue + PR |
| 单模块内非破坏性 API 新增 | ❌ Issue + PR（Design Doc 记录） |
| 文档/测试/重构 | ❌ Issue + PR |
| 工程整改（CI、构建脚本） | ⚠️ 视影响面，由维护者判断 |

### 2.3 文件命名

```
docs/rfc/<PascalCaseName>.md
```

### 2.4 Frontmatter 标准

```markdown
> **状态**：Draft | Review | Accepted | Rejected | Implemented | Superseded
> **类型**：Feature | Architecture | Process
> **创建**：YYYY-MM-DD
> **更新**：YYYY-MM-DD
> **作者**：维护者 / 贡献者
> **关联 Roadmap**：条目 ID（如有）
> **关联 Issue**：#XXX（如有）
> **衍生 ADR**：ADR-XXX（Accepted 后填写）
```

### 2.5 生命周期

```
Draft → Review → Accepted → Implemented → (archive)
                ↘ Rejected → (archive)
```

归档至 `docs/rfc/archive/`。详见 [docs/rfc/README.md](rfc/README.md)。

---

## 3. ADR — Architecture Decision Record

### 3.1 用途

记录**最终架构决策**的简短不可变卡片。讨论在 RFC 中完成，ADR 只记录结论。

### 3.2 文件命名

```
docs/adr/ADR-<NNN>-<kebab-case-title>.md
```

编号从 `001` 开始，零填充三位，**不复用编号**。下一个可用编号见 [docs/adr/README.md](adr/README.md)。

### 3.3 不可变原则

- ADR 一旦 **Accepted**，正文**不修改**。
- 决策被推翻时创建新 ADR，旧 ADR 状态改为 `Superseded by ADR-XXX`。

---

## 4. Spec — 规范文档

定义子系统的**稳定契约**：公共 API 面、工具 schema、Skill 清单、不变量。描述 **what**，不描述 **how**。

- 模板：[docs/spec/_template.md](spec/_template.md)
- 索引：[docs/spec/README.md](spec/README.md)

### 变更门槛

- **新增工具 / Skill** 或 **破坏性变更**：RFC → ADR → Spec 更新 + CHANGELOG。
- **措辞修正**：直接 PR。

---

## 5. Design Doc — 设计文档

记录**实现细节**、设计权衡、已知局限。描述 **how** 和 **why**。

- 模板：[docs/design/_template.md](design/_template.md)
- 索引：[docs/design/README.md](design/README.md)

随代码 PR 同步更新；若导致 Spec 契约变更，须走 RFC 流程。

---

## 6. Roadmap

维护 [docs/ROADMAP.md](ROADMAP.md) 作为功能与技术 backlog 的滚动清单。

```
候选 → 排期 → 进行中 → 已完成（归档）
                    ↘ 暂缓 / 明确不做
```

技术债详见根目录 [ISSUES.md](../ISSUES.md)，Roadmap 引用但不重复全文。

---

## 7. Plan — 任务计划

### 7.1 载体（双轨）

| 规模 | 载体 |
|------|------|
| 跨多 PR、多里程碑 | `docs/plans/<PascalCaseName>.md` |
| 单 PR 可完成 | GitHub Issue |

模板：[docs/plans/_template.md](plans/_template.md)

### 7.2 生命周期

```
Active → Done → archive/
       ↘ Cancelled → archive/
```

---

## 8. Review — 评审记录

记录**结构化评审结论**（跨 PR 或里程碑级）。

- 模板：[docs/review/_template.md](review/_template.md)
- 单 PR code review 用 PR Comments，无需 Review 文档。

---

## 9. 归档机制（统一规则）

| 类型 | 归档目录 | 归档触发 |
|------|----------|----------|
| RFC | `docs/rfc/archive/` | Implemented / Rejected / Superseded |
| Plan | `docs/plans/archive/` | Done / Cancelled |
| Review | `docs/review/archive/` | Final 且行动项全部关闭 |
| ADR | 不移动 | Supersede 原地改状态 |
| Spec / Design Doc | 不归档 | 活文档，随实现演进 |

---

## 10. 目录结构

```
docs/
├── DOCUMENTATION.md          # 本文件
├── README.md                 # 文档索引
├── DEVELOPMENT.md            # 开发手册
├── ROADMAP.md                # 路线图
├── rfc/  adr/  spec/  design/  plans/  review/
│   └── archive/              # 各类型归档（RFC / Plan / Review）
```

---

## 11. 文档驱动开发流程

**先文档后代码**：动手写代码前，先按下表判定变更所需的文档前置条件。

### 11.1 变更类型 → 文档前置条件决策表

| 变更类型 | RFC | ADR | Plan | Review | 实现 PR 须同步 |
|----------|-----|-----|------|--------|----------------|
| 新增 Agent 工具 / Skill | ✅ Accepted | ✅ | 视规模 | ✅ 设计评审 | Spec 更新 + Design Doc + CHANGELOG |
| 破坏性公共 API 变更 | ✅ Accepted | ✅ | 视规模 | ✅ | Spec + CHANGELOG `Breaking` |
| 非破坏性 API 新增（单模块） | ❌ | ❌ | ❌ | ❌ | Design Doc + CHANGELOG |
| Bug fix | ❌ | ❌ | ❌ | ❌ | CHANGELOG（如用户可见） |
| 重构（无行为变更） | ❌ | 视架构影响 | ❌ | ❌ | Design Doc（如结构变化） |
| 发版 | ❌ | ❌ | ❌ | ✅ Release 审查 | CHANGELOG 版本化 |

### 11.2 新功能完整流程

```
1. Roadmap 候选 → 排期
2. 创建 RFC（Draft）→ Review → Accepted → ADR
3. 大型任务：docs/plans/ + 主 Issue
4. 实现 PR：同步更新 Spec + Design Doc
5. 合并 → RFC Implemented → 归档；Plan Done → 归档
6. CHANGELOG + Roadmap 更新
```

### 11.3 Agent 工作流约定

| 场景 | Agent 行为 |
|------|-----------|
| 新增工具 / Skill | 确认 RFC + ADR；无则提示创建 |
| 跨多 PR 大型任务 | 先建 Plan（经用户确认） |
| 创建 ADR | 编号取 `docs/adr/README.md` 下一个可用 |
| Spec 变更 | 确认 RFC 已 Accepted |
| Design Doc | 随代码 PR 同步 |
| CHANGELOG | `[Unreleased]` 添加条目 |
| 文档位置 | 仅 `docs/`（产品知识在 `Resources/`） |

### 11.4 模块 PR 边界

与 `AGENTS.md` 一致：尽量单模块单 PR（`IsaacAgent.App` / `Core` / `LLM` / `Tools` / `Agent` / `Rag`）。

---

## 12. 文档与 Git 的关系

| 文档类型 | 提交方式 |
|----------|----------|
| RFC / ADR / Plan / Review | 独立 PR 或随实现 PR |
| Spec / Design Doc | 与代码变更同一 PR |
| Roadmap / CHANGELOG | 随实现 PR |

### 12.1 PR 模板 checklist

见 [.github/PULL_REQUEST_TEMPLATE.md](../.github/PULL_REQUEST_TEMPLATE.md)。

---

## 13. 文档质量检查清单

- [ ] 文件位置与命名符合 §10
- [ ] Frontmatter 完整
- [ ] 交叉链接有效（RFC ↔ ADR ↔ Spec ↔ Design Doc）
- [ ] `docs/README.md` 及相关 README 索引已更新
- [ ] 无 AI/LLM 工具名称、无私有工作区路径

---

## 14. 参考

- [Skymly/DesignPatterns 文档体系](https://github.com/Skymly/DesignPatterns/blob/main/docs/DOCUMENTATION.md)
- [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
- [ADR 格式](https://adr.github.io/)

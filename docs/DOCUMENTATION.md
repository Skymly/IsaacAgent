# 文档约定

> 仓内文档只沉淀**跨 Issue/PR 的累积知识**与**不应随讨论漂移的决策**。
> 需求讨论、任务追踪、变更审查、版本历史分别用 GitHub Issue / PR / Release。
> 冲突优先级：`AGENTS.md` > `docs/DOCUMENTATION.md` > 其他文档。

## 文档载体

| 载体 | 位置 | 用途 |
|------|------|------|
| **ADR** | `docs/adr/` | 架构决策（不可变卡片） |
| **Design Doc** | `docs/design/` | 每模块一份：API 面 + 不变量 + 实现细节 + 设计权衡 |
| **Roadmap** | `docs/ROADMAP.md` | 宏观规划排序 |
| **Issue** | GitHub Issues | 需求、Bug、任务追踪 |
| **PR** | GitHub Pull Requests | 变更审查 |
| **Release** | GitHub Releases + `CHANGELOG.md` | 版本历史 |

### 不纳入仓内文档体系

| 内容 | 载体 |
|------|------|
| 编码规范、构建、模块边界 | `AGENTS.md` |
| 开发环境 | `docs/DEVELOPMENT.md` |
| 贡献流程 | `CONTRIBUTING.md` |
| 技术债审计 | `ISSUES.md` |
| 产品知识（Isaac API） | `src/IsaacAgent.Rag/Resources/` |

## ADR

- 编号：`ADR-NNN-<kebab-title>.md`，从 001 起，**不复用**
- Accepted 后正文**不可变**；推翻决策时新建 ADR，旧卡标记 `Superseded by ADR-XXX`
- 模板：[docs/adr/_template.md](adr/_template.md)
- 下一个可用编号见 [docs/adr/README.md](adr/README.md)

### 何时写 ADR

- 跨模块架构变更（依赖方向、分层边界）
- 新增 / 变更核心能力策略（如 RAG 存储、Skill 层、平台目标）
- 否决某个技术方向（记录为什么不做）
- 破坏性公共契约变更（`ITool` / `ISkill` / 配置面）

## Design Doc

- 每模块一份（Agent / Tools / Rag / App …）
- 含：API 面、不变量、实现概览、设计权衡、已知局限
- **随实现 PR 同步更新**
- 模板：[docs/design/_template.md](design/_template.md)

## 变更时更新什么

| 变更 | 更新 |
|------|------|
| 破坏性架构决策 | ADR + 相关 Design Doc |
| 工具 / Skill / API / 实现细节 | Design Doc |
| 用户可见行为 | CHANGELOG `[Unreleased]` |
| 宏观优先级 | ROADMAP |

## 目录结构

```
docs/
├── DOCUMENTATION.md
├── README.md
├── DEVELOPMENT.md
├── ROADMAP.md
├── adr/
└── design/
```

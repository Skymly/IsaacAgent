# IsaacAgent 文档索引

维护者文档入口。完整标准见 [DOCUMENTATION.md](DOCUMENTATION.md)。

## 快速导航

| 文档 | 说明 |
|------|------|
| [DOCUMENTATION.md](DOCUMENTATION.md) | 文档体系标准（权威） |
| [DEVELOPMENT.md](DEVELOPMENT.md) | 开发环境、构建、测试 |
| [ROADMAP.md](ROADMAP.md) | 功能与技术 backlog |
| [../AGENTS.md](../AGENTS.md) | Agent / 维护者速查（构建、模块边界） |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | 贡献流程 |
| [../ISSUES.md](../ISSUES.md) | 技术债审计清单 |
| [../CHANGELOG.md](../CHANGELOG.md) | 版本变更记录 |

## 七层文档

| 层 | 目录 | 索引 |
|----|------|------|
| RFC | [rfc/](rfc/) | [rfc/README.md](rfc/README.md) |
| ADR | [adr/](adr/) | [adr/README.md](adr/README.md) |
| Spec | [spec/](spec/) | [spec/README.md](spec/README.md) |
| Design Doc | [design/](design/) | [design/README.md](design/README.md) |
| Roadmap | [ROADMAP.md](ROADMAP.md) | — |
| Plan | [plans/](plans/) | [plans/README.md](plans/README.md) |
| Review | [review/](review/) | [review/README.md](review/README.md) |

## 子系统文档

| 子系统 | Spec | Design Doc |
|--------|------|------------|
| Agent | [spec/Agent.md](spec/Agent.md) | [design/Agent.md](design/Agent.md) |
| Tools | [spec/Tools.md](spec/Tools.md) | [design/Tools.md](design/Tools.md) |
| Rag | [spec/Rag.md](spec/Rag.md) | [design/Rag.md](design/Rag.md) |
| App | — | [design/App.md](design/App.md) |

## 与产品知识的区分

| 位置 | 用途 | 受众 |
|------|------|------|
| `docs/`（本目录） | 架构、契约、开发流程 | 维护者、贡献者 |
| `src/IsaacAgent.Rag/Resources/` | Isaac API、Mod 模式、XSD | RAG 检索 + Agent 终端用户 |

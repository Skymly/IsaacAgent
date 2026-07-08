# ADR-004: Skill layer above atomic tools

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-15 |
| **关联 RFC** | 无 — 直接决策 |

## 背景

纯 Tool Calling 对 Isaac Mod 多步工作流（创建道具、调试 log、验证 XML）需要用户反复提示；希望有更高层的任务引导与 RAG 预取。

## 决策

在 System Prompt 与原子 `ITool` 之间引入 **Skill** 层（`ISkill`）：

- 10 个内置 Skill（如 `/create-item`、`/debug`、`/validate`）
- 通过关键词或 Slash 命令激活
- 注入任务专用 prompt 片段并预取 RAG 上下文

Skill 不替代 Tool；LLM 仍通过 ToolRegistry 调用原子工具。

## 后果

- 正面：常见 Mod 任务开箱即用；减少用户提示工程。
- 负面：Skill 与 prompt 需与 API 知识库同步维护；新增 Skill 需 Spec 更新。

## 参考

- [spec/Agent.md](../spec/Agent.md)
- [design/Agent.md](../design/Agent.md)

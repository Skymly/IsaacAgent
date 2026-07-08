# Design Doc: Tools

> **关联 Spec**：[docs/spec/Tools.md](../spec/Tools.md)

## 概述

`IsaacAgent.Tools` 提供通用文件/项目/知识工具；`IsaacAgent.Rag` 提供 RAG 相关工具。全部通过 `ToolRegistry` 统一暴露给 LLM。

## 设计目标

- 项目目录沙箱：LLM 不能读写项目外路径
- Isaac 领域专用：scaffold、diagnose、API 查询、XML 校验、log 解析
- 可独立单元测试（mock 项目目录）

## 实现概览

### 路径安全

`FileToolPathSafety.IsWithinProject`：

- 构造时 `Path.GetFullPath` 规范化项目根
- 根目录末尾追加 `Path.DirectorySeparatorChar` 防 sibling-prefix 穿越
- `list_files` 跳过 reparse point（junction/symlink）

### 代表性工具

| 工具 | 要点 |
|------|------|
| `scaffold_mod` | 生成 `metadata.xml` + `main.lua`；XML/Lua 转义 |
| `diagnose_lua` | 剥离字符串/注释后检查括号、未知回调 |
| `run_command` | 按 `&&`/`\|\|` 分割子命令；`GIT_TERMINAL_PROMPT=0` 等 git 加固 |
| `parse_log` | 绝对路径仅允许默认 Isaac log；相对路径项目内解析 |
| `diff_apply` | unified diff 解析与应用 |

### 注册

`AgentServiceRegistration` 在 DI 中注册全部 `ITool` 单例；`ToolRegistry` 构造时收集。

## 设计权衡

- **内置 API 知识 vs RAG**：`search_isaac_api` 用结构化字典（快、确定）；`search_knowledge` 用语义检索（广、需索引）。
- **run_command 能力**：提供强大调试能力，但以危险命令黑名单约束。

## 已知局限

- `batch_edit` 无事务回滚
- `git_status` 依赖本机 git 可用

## 参考

- `src/IsaacAgent.Tools/Implementations/`
- `src/IsaacAgent.Rag/Tools/`

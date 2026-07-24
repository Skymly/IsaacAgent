# Design Doc: Tools

> **关联 ADR**：[ADR-001](../adr/ADR-001-multi-project-layered-architecture.md)

## 概述

`IsaacAgent.Tools` 提供通用文件/项目/知识工具；`IsaacAgent.Rag` 提供 RAG 相关工具。全部通过 `ToolRegistry` 统一暴露给 LLM。

## 设计目标

- 项目目录沙箱：LLM 不能读写项目外路径
- Isaac 领域专用：scaffold、diagnose、API 查询、XML 校验、log 解析
- 可独立单元测试（mock 项目目录）

## API 面

所有工具实现 `ITool`：`Name`、`Description`、`Definition`（JSON schema）、`ExecuteAsync(arguments, ct)`。

### IsaacAgent.Tools（12）

| Name | 类 | 说明 |
|------|-----|------|
| `read_file` | `ReadFileTool` | 读取项目内文件 |
| `write_file` | `WriteFileTool` | 写入项目内文件 |
| `list_files` | `ListFilesTool` | 列出项目内文件树 |
| `scaffold_mod` | `ScaffoldModTool` | 脚手架生成 Mod 结构 |
| `diagnose_lua` | `DiagnoseLuaTool` | 静态分析 Lua 语法/回调 |
| `search_isaac_api` | `SearchApiTool` | 搜索内置 API 知识 |
| `get_callback_info` | `GetCallbackInfoTool` | 查询 ModCallbacks |
| `get_class_info` | `GetClassInfoTool` | 查询 API 类信息 |
| `git_status` | `GitStatusTool` | Git 工作区状态 |
| `diff_apply` | `DiffApplyTool` | 应用 unified diff |
| `batch_edit` | `BatchEditTool` | 批量编辑多文件 |
| `run_command` | `RunCommandTool` | 运行 shell 命令（有危险命令拦截） |

### IsaacAgent.Rag（4）

| Name | 类 | 说明 |
|------|-----|------|
| `search_knowledge` | `SearchKnowledgeTool` | RAG 语义检索 |
| `get_pattern` | `GetPatternTool` | 获取 Mod 模式模板 |
| `validate_xml` | `ValidateXmlTool` | XSD 校验 entities XML |
| `parse_log` | `ParseLogTool` | 解析 Isaac `log.txt` |

## 不变量

1. **路径安全**：`read_file` / `write_file` / `list_files` / `diagnose_lua` / `validate_xml` / `parse_log`（相对路径）必须通过 `FileToolPathSafety.IsWithinProject` 或等效白名单检查。
2. **用户输入转义**：`scaffold_mod` 的 XML 字段须 `SecurityElement.Escape`；Lua 模板字符串须转义。
3. **危险命令**：`run_command` 拦截 `rm -rf`、`format`、PowerShell 危险模式等。
4. 工具注册在 `ToolRegistry`；reconfigure 与 lookup 须线程安全（`SemaphoreSlim`）。

## Checkpoint：Tracked write 与可观察性

核心合同见 [Agent.md](Agent.md) Checkpoint 节与 `CONTEXT.md`。Tools 侧约定：

| 合同类 | 工具 | 说明 |
|--------|------|------|
| **Tracked write** | `write_file`、`diff_apply`、`batch_edit`、`scaffold_mod` | 懒 Before-image 义务；捕获在 **Agent** `ToolRegistry.ExecuteAsync`，**不**要求改各 `ITool` 实现体 |
| **Untracked** | `run_command` | 可改盘，**不**保证 Restore |

- **`scaffold_mod`**：多文件按路径各自跟踪（非整次调用原子回滚）；路径推导使用与工具一致的固定输出名列表。
- **`batch_edit`**：仍无事务；Before-image 按路径首次触碰捕获（Registry 宜预扫 `edits[].path`）。
- **可观察性**：Checkpoint / Restore 生命周期日志在 Agent；Tools Design Doc 仅记录上表分类，便于工具作者勿把 `run_command` 当成可恢复写入。若未来在 Tools 模块增加显式钩子，再单开 Tools PR。
- 清单研究：[checkpoint-tracked-write-hooks.md](../research/checkpoint-tracked-write-hooks.md)。

## 实现概览

### 路径安全

`FileToolPathSafety.IsWithinProject`：

- 构造时 `Path.GetFullPath` 规范化项目根
- 根目录末尾追加 `Path.DirectorySeparatorChar` 防 sibling-prefix 穿越
- `list_files` 跳过 reparse point（junction/symlink）

### 代表性工具

| 工具 | 要点 |
|------|------|
| `scaffold_mod` | 生成 `metadata.xml` + `main.lua`；XML/Lua 转义；Checkpoint 下按路径 Tracked |
| `diagnose_lua` | 剥离字符串/注释后检查括号、未知回调 |
| `run_command` | 按 `&&`/`\|\|`/`|`/`;` 分割子命令；扩展黑名单（encoded PowerShell、certutil、LOLBins）；`GIT_TERMINAL_PROMPT=0` / `GCM_INTERACTIVE=never` 防交互挂起；命令长度上限 4096；**Checkpoint untracked** |
| `parse_log` | 绝对路径仅允许默认 Isaac log；相对路径项目内解析 |
| `diff_apply` | unified diff 解析与应用；Tracked write |

### 注册

`AgentServiceRegistration` 在 DI 中注册全部 `ITool` 单例；`ToolRegistry` 构造时收集。

## 设计权衡

- **内置 API 知识 vs RAG**：`search_isaac_api` 用结构化字典（快、确定）；`search_knowledge` 用语义检索（广、需索引）。
- **run_command 能力**：提供强大调试能力，但以危险命令黑名单约束；与 Checkpoint 合同一致——不假装可 Restore。

## 兼容基线

- 工具 schema 以 OpenAI function calling 格式暴露给 LLM
- 项目目录由 `AgentSession` / `ToolRegistry` 注入

## 不在范围内

- Skill 层工作流（见 [Agent.md](Agent.md)）
- RAG 索引构建（见 [Rag.md](Rag.md)）
- Checkpoint 捕获实现（Agent `ToolRegistry` 缝）

## 已知局限

- `batch_edit` 无事务回滚（与 Checkpoint 按路径 Before-image 一致，非整批原子）
- `git_status` 依赖本机 git 可用

## 参考

- `src/IsaacAgent.Tools/Implementations/`
- `src/IsaacAgent.Rag/Tools/`
- [Agent.md](Agent.md) Checkpoint 合同
- [docs/research/checkpoint-tracked-write-hooks.md](../research/checkpoint-tracked-write-hooks.md)

# Spec: Tools

> **版本**：v0.2.x（与 Git tag 对齐）
> **关联 Design Doc**：[docs/design/Tools.md](../design/Tools.md)
> **关联 ADR**：[ADR-001](../adr/ADR-001-multi-project-layered-architecture.md)

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

## 兼容基线

- 工具 schema 以 OpenAI function calling 格式暴露给 LLM
- 项目目录由 `AgentSession` / `ToolRegistry` 注入

## 不在范围内

- Skill 层工作流（见 [spec/Agent.md](Agent.md)）
- RAG 索引构建（见 [spec/Rag.md](Rag.md)）

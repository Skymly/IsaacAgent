# IsaacAgent — 问题审计清单

> 首次审计日期：2026-06-22
> 审计基线：`main` 分支（commit `165987c`）
> 状态约定：[ ] 待处理 / [~] 进行中 / [x] 已修复

---

## P0 — 阻断构建，必须立即修复

### P0-1 `MarkdownRenderer` 使用了 Avalonia 不存在的 `Run.Underline` 属性，导致 main 分支构建失败  [x] 已修复

- 文件：`src/IsaacAgent.App/Markdown/MarkdownRenderer.cs:263`
- 现象：`dotnet build` 报 `CS0117: "Run"未包含"Underline"的定义`，CI（`.github/workflows/build-and-test.yml`）必然红。
- 根因：`Underline` 是 WPF API，Avalonia 11 的 `Run` 没有该属性。
- 修复方案：改为 `TextDecorations = TextDecorations.Underline`（来自 `Avalonia.Media`）。
- 影响范围：仅链接渲染分支。
- 验证：`dotnet build IsaacAgent.sln -c Release` 通过；`dotnet test` 通过（101 通过 / 0 失败 / 0 跳过）。

---

## P1 — 安全/正确性，优先修复

### P1-1 `FileTools` 三个工具的路径穿越检查不完整  [x] 已修复

- 文件：`src/IsaacAgent.Tools/Implementations/FileTools.cs`
  - `ReadFileTool.ExecuteAsync` (约 36 行)
  - `WriteFileTool.ExecuteAsync` (约 77 行)
  - `ListFilesTool.ExecuteAsync` (约 125 行)
- 现象：使用 `fullPath.StartsWith(_projectDir, OrdinalIgnoreCase)` 判断越界。
  当 `_projectDir = "C:\Mods\Foo"` 时，`"C:\Mods\FooBar\secret.lua".StartsWith("C:\Mods\Foo")` 返回 `true`，
  可通过 `path: "../FooBar/secret"` 逃逸到项目外。
- 对比：`DiagnoseLuaTool` 与 `ValidateXmlTool` 已修复（追加 `Path.DirectorySeparatorChar`），`FileTools` 未同步。
- 修复方案：与 `DiagnoseLuaTool` 对齐，构造 `projectRoot = _projectDir.EndsWith(Separator) ? _projectDir : _projectDir + Separator`，再用 `fullPath.StartsWith(projectRoot, OrdinalIgnoreCase)`。
  新增 `FileToolPathSafety.IsWithinProject` 共享 helper；构造时 `Path.GetFullPath` 规范化。
- 风险：LLM 若被诱导调用 `read_file { path: "../<邻居项目>/main.lua" }` 可读取项目外文件（只读，但仍是越权）。
- 验证：新增 6 个测试（Read/Write/List 各 2：sibling-prefix 拒绝 + valid 通过），全部通过。总 107 测试通过。

### P1-2 `ScaffoldModTool` 生成的 XML 未对用户输入转义  [x] 已修复

- 文件：`src/IsaacAgent.Tools/Implementations/ScaffoldModTool.cs:65-74`
- 现象：`{name}` / `{description}` / `{author}` 直接插入 `metadata.xml`，未做 XML 转义。
  含 `<` `&` `>` 的输入会生成无效 XML，且随后 `validate_xml` 会报一堆莫名错误。
- 修复方案：用 `System.Security.SecurityElement.Escape(...)` 包裹三个字段。
- 附带：`main.lua` 模板里 `{name}` 插入到 Lua 字符串字面量，含 `"` `\` 的输入会破坏 Lua 语法，需 Lua 字符串转义。
  新增 `EscapeLuaString` helper，转义 `\` `"` `\n` `\r` `\t`。
- 验证：新增 4 个测试（XML 转义、Lua 双引号转义、Lua 反斜杠转义、普通名生成），全部通过。总 111 测试通过。

### P1-3 `ParseLogTool` 接受任意绝对路径，无越界限制  [x] 已修复

- 文件：`src/IsaacAgent.Rag/Tools/ParseLogTool.cs:75-85`
- 现象：`file_path` 接受任意绝对路径并 `File.ReadAllText`，LLM 可被诱导读 `C:\Windows\...` 等敏感文件。
- 修复方案：若 `Path.IsPathRooted(path)`，限制为「默认 Isaac log 路径」白名单；否则只允许相对路径解析到项目内，并做穿越检查。
  `ResolveLogPath` 改为返回 `(string? Path, string? Error)` 元组。
- 验证：新增 3 个测试（绝对路径拒绝、路径穿越拒绝、sibling-prefix 拒绝），全部通过。总 114 测试通过。

---

## P2 — 设计/正确性，中优先级

### P2-1 `OpenAICompatibleProvider.StreamAsync` 只读取 `tool_calls[0]`  [x] 已修复

- 文件：`src/IsaacAgent.LLM/Providers/OpenAICompatibleProvider.cs:81-92`
- 现象：单个 delta chunk 内若同时携带多个 tool call（部分 OpenAI 兼容端点会这么做），后续的会丢失。
  `AgentSession` 的累加器按 index 分桶跨 chunk 是 OK 的，但单 chunk 多 call 会有问题。
- 修复方案：遍历 `tc.EnumerateArray()`，对每个元素 yield 一个 `ChatChunk`。

### P2-2 `OpenAICompatibleProvider.BuildPayload` 始终发送 `temperature` / `max_tokens` / `tools: null`  [x] 已修复

- 文件：`src/IsaacAgent.LLM/Providers/OpenAICompatibleProvider.cs:102-137`
- 现象：某些 OpenAI 兼容端点（部分 Ollama OpenAI-compat 模式、MiniMax 早期版本）对 `tools: null` 或未知字段敏感，可能报错或行为异常。
- 修复方案：改用 `Dictionary<string, object?>` 构建 payload，空集合时省略 `tools` 字段；消息级省略 `tool_calls: null` / `tool_call_id: null`。

### P2-3 `OllamaProvider.BuildPayload` 在每条消息上都带 `tool_calls: null` / `tool_call_id: null`  [x] 已修复

- 文件：`src/IsaacAgent.LLM/Providers/OllamaProvider.cs:102-113`
- 现象：对 user/system 消息也发送这些字段，部分 Ollama 版本会报错或行为异常。
- 修复方案：仅当 `m.ToolCalls.Count > 0` 时发送 `tool_calls`，仅当 `m.ToolCallId` 非空时发送 `tool_call_id`。同样改用 `Dictionary` 构建 payload，空 tools 时省略。

### P2-4 `OnnxEmbeddingProvider` 硬编码 `_dimensions = 384`  [x] 已修复

- 文件：`src/IsaacAgent.Rag/Embedding/OnnxEmbeddingProvider.cs:29`
- 现象：换其它 ONNX 嵌入模型会维度不匹配，导致 `InMemoryVectorStore` 检测到维度不一致后强制重建索引，但 `_dimensions` 始终是 384，与实际模型不符时检索结果全错。
- 修复方案：构造时从 `OutputMetadata` 推断维度，失败时跑 dummy forward pass 推断，最终 fallback 仍为 384。

### P2-5 `OnnxEmbeddingProvider.EmbedBatchAsync` 没有真正批处理  [x] 已修复

- 文件：`src/IsaacAgent.Rag/Embedding/OnnxEmbeddingProvider.cs:42-54`
- 现象：循环里逐条 `EmbedSingle` 跑 ONNX 推理，没有把多条 pad 成一个 batch tensor 一次前向。
  构建 453 文档索引会很慢。
- 修复方案：tokenize 全部文本后 pad 到 batch 内最大 seqLen，组成 `[batch, seqLen]` tensor 一次 `session.Run`，再按 batch index 拆分输出做 MeanPool+Normalize。

### P2-6 `IndexBuilder.BuildAsync` 使用 `Skip(i).Take(batch)`，整体 O(n²)  [x] 已修复

- 文件：`src/IsaacAgent.Rag/Indexing/IndexBuilder.cs:60-65`
- 现象：每轮 `Skip(i)` 是 O(n)，整体 O(n²)。对几千 chunk 尚可，但增长后会变慢。
- 修复方案：改用 `chunks.GetRange(i, take)`，O(1) 切片。

### P2-7 首次 `search_knowledge` 调用会阻塞 UI  [x] 已修复

- 文件：`src/IsaacAgent.Rag/Retrieval/Retriever.cs:83-90`、`src/IsaacAgent.Agent/Engine/AgentSession.cs:113`
- 现象：`Retriever.SearchAsync` 先 `await EnsureIndexAsync`，首次调用会同步构建索引
  （ONNX 路径可能几十秒）。`AgentSession` 在 tool 执行里 `await`，UI 通过 `IsGenerating`
  看到的是「正在生成」但无进度提示。
- 修复方案：`App.OnFrameworkInitializationCompleted` 后 fire-and-forget `PrewarmRagIndexAsync`，预热失败只 log warning 不影响启动。

### P2-8 `App.ReloadEmbeddingProvider` 的 fire-and-forget 吞掉异常  [x] 已修复

- 文件：`src/IsaacAgent.App/App.cs:92-103`
- 现象：异常只 `Debug.WriteLine`，用户在 Settings 里切换嵌入源后看不到错误；
  且用默认 CT，无法取消。
- 修复方案：异常路由到 `SettingsViewModel.SetIndexStatus`，通过 `SetIndexRebuilding` 控制 UI 状态。两个方法内部 `Dispatcher.UIThread.Post` 保证线程安全。

### P2-9 `AgentSession.TrimHistory` 仅处理「头部孤儿 tool 结果」  [x] 已修复

- 文件：`src/IsaacAgent.Agent/Engine/AgentSession.cs:180-201`
- 现象：注释说「不要在 tool_call / tool_result 对中间切」，但实现只检查 `_history[1].Role == "tool"` 这一种孤儿情况。
  若裁剪后新首条是带 `ToolCalls` 的 assistant 消息、而对应 tool 结果被裁掉了，
  下一轮请求会因「assistant 有 tool_calls 但无 tool 结果」被部分 API 拒绝。
- 修复方案：`TrimHistory` 扩展为处理两种孤儿情况：(1) 头部 orphaned tool result；(2) assistant 带 tool_calls 但对应 tool results 不完整的整组删除。

### P2-10 `AppConfiguration` 明文存储 `ApiKey`  [x] 已修复

- 文件：`src/IsaacAgent.App/Services/AppConfiguration.cs:56-67`
- 现象：`%APPDATA%\IsaacAgent\config.json` 明文存储 API Key。
- 修复方案：新增 `System.Security.Cryptography.ProtectedData` NuGet 包，`Save` 时用 DPAPI `ProtectedData.Protect`（CurrentUser scope）加密 ApiKey 为 base64 存入 `EncryptedApiKey` 字段，`ApiKey` 字段不序列化。`Load` 时 `ProtectedData.Unprotect` 解密。DPAPI 不可用时 fallback 明文（非 Windows）。解密失败时清空 key 让用户重新输入。

### P2-11 `Avalonia.Diagnostics` 在 Release 也被引用  [x] 已修复

- 文件：`src/IsaacAgent.App/IsaacAgent.App.csproj:16`
- 现象：发布包会带上调试 overlay。
- 修复方案：改为 `<ItemGroup Condition="'$(Configuration)' == 'Debug'">` 包裹 `Avalonia.Diagnostics` 引用。Release 构建已验证通过。

---

## P3 — 工程化短板

### P3-1 CI 缺少 lint / format / 发布 / 版本号 / 打包步骤  [x] 已修复

- 文件：`.github/workflows/build-and-test.yml`
- 现状：只跑 `windows-latest` 的 `dotnet build` + `dotnet test`。
- 修复：
  - 新增 `format-check` job：`dotnet format --verify-no-changes`
  - `build-and-test` 改为矩阵 `windows-latest / ubuntu-latest / macos-latest`
  - 新增 `release-build` job：Release 构建 + `dotnet publish` 单文件可执行 + upload artifact

### P3-2 缺少 `Directory.Build.props` 统一工程属性  [x] 已修复

- 现状：每个 csproj 各写一遍 `TargetFramework` / `ImplicitUsings` / `Nullable` / `LangVersion`。
- 修复：新增 `Directory.Build.props` 统一 4 个属性 + Release `TreatWarningsAsErrors=true`。8 个 csproj 全部简化移除重复属性。App 项目加 `SupportedOSPlatform=windows`。

### P3-3 没有 ViewModel / View 层测试  [x] 已修复

- 现状：101 个测试方法集中在 Agent / LLM / RAG / Tools，UI 层零覆盖。
- 修复：Tests 项目添加 `Avalonia.Headless` + App 项目引用。新增 `ProjectViewModelTests`（6 个测试：LoadProject/NonexistentDir/EmptyDir/OpenFileAsync/NullItem/FileTreeItem）和 `ChatMessageViewModelTests`（10 个测试：RoleLabel/IsUser/IsAssistant/IsError/IsSystem/Content debounce）。+16 测试，130 全过。

### P3-4 README 与代码轻微漂移  [x] 已修复

- 文件：`README.md:41-50` vs `src/IsaacAgent.App/Services/AppConfiguration.cs:42-51`
- 现象：README 说「Set environment variables」列出三个 `ISAAC_AGENT_*`，但代码只在 `ISAAC_AGENT_API_KEY` 存在时才读 `ENDPOINT` / `MODEL` 环境变量。
- 修复：README 更新说明 `ISAAC_AGENT_API_KEY` 是必需的，`ENDPOINT` / `MODEL` 有默认值。config.json 例子改为 `EncryptedApiKey`（DPAPI 加密），说明由 Settings UI 管理。

### P3-5 `AgentSession` / `ChatViewModel` 注册为 Singleton，阻碍多会话扩展  [x] 已修复

- 文件：`src/IsaacAgent.Agent/AgentServiceRegistration.cs:21-27`、`src/IsaacAgent.App/App.cs:65`
- 现状：`AgentSession` 内部 `_projectDir` / `_history` 是实例字段，`ChatViewModel` 在构造函数订阅其事件且永不取消订阅。
- 修复：引入 `IAgentSessionFactory` + `AgentSessionFactory`，`AgentSession` 改为 `Transient`。`ChatViewModel` 实现 `IDisposable`，`OnProjectChanged` 时正确取消旧 session 事件订阅并创建新 session。

### P3-6 `DiagnoseLuaTool` 行级正则分析误报率高  [x] 已修复

- 文件：`src/IsaacAgent.Tools/Implementations/DiagnoseLuaTool.cs:58-196`
- 现象：没有 Lua AST，行级正则易误报：
  - `^\s*(\w+)\s*=\s*[^=]` 触发「全局变量」警告会误报 table 字段赋值、`for` 变量、多行 table 构造里的键值。
  - 「双引号不匹配」检查在字符串含转义引号或跨 `--` 切分时会误报。
  - `IsBuiltInGlobal` 白名单只是部分缓解。
- 修复：新增 `StripStringsAndComments` 函数，括号/花括号/方括号平衡检查改为在剥离字符串和注释后的代码上计算，消除字符串/注释内括号的误报。引号检测改用 `CountUnescapedQuotes`（转义感知）+ `FindCommentStart`（跳过注释）。`AddCollectible` 检查改为计数逗号而非要求 `, `。`debug 7` 检查改为精确匹配 `print("debug 7")` 模式。全局变量检测改用 `codeLine`（字符串剥离后）。+9 误报减少测试，139 全过。

---

## 修复优先级建议

1. **P0-1** 修 `MarkdownRenderer` 的 `Underline` → `TextDecorations = TextDecorations.Underline`，让 main 重新可构建、CI 转绿。
2. **P1-1** 修 `FileTools` 三个工具的路径穿越。
3. **P1-2** 修 `ScaffoldModTool` XML 转义 + Lua 字符串转义。
4. **P1-3** 修 `ParseLogTool` 路径白名单。
5. **P2-1 ~ P2-3** LLM provider 的多 tool_call / 空字段处理。
6. **P2-4 ~ P2-6** ONNX 维度推断 + 真批处理 + `IndexBuilder` O(n²) 修复。
7. **P2-7** 启动时后台预热 RAG 索引。
8. **P2-10** ApiKey 用 DPAPI 保护。
9. **P3** 工程化补齐。

---

## 修复记录

| 日期 | 问题编号 | 修复人 | 备注 |
|------|----------|--------|------|
| 2026-06-22 | P0-1 | - | `Underline = true` → `TextDecorations = TextDecorations.Underline`；Release 构建通过，101 测试全过 |
| 2026-06-22 | P1-1 | - | `FileTools` 三个工具路径穿越修复：`FileToolPathSafety.IsWithinProject` + 构造时 `Path.GetFullPath`；+6 测试，107 全过 |
| 2026-06-22 | P1-2 | - | `ScaffoldModTool` XML 转义（`SecurityElement.Escape`）+ Lua 字符串转义（`EscapeLuaString`）；+4 测试，111 全过 |
| 2026-06-22 | P1-3 | - | `ParseLogTool` 绝对路径白名单（仅默认 Isaac log）+ 相对路径穿越检查；+3 测试，114 全过 |
| 2026-06-22 | P2-1~P2-3 | - | LLM provider 修复：OpenAI 多 tool_call 遍历 + 两家 provider 空字段省略（Dictionary payload）；114 测试全过 |
| 2026-06-22 | P2-4~P2-5 | - | ONNX 维度从模型输出推断 + 真正批处理（pad + 单次 forward）；114 测试全过 |
| 2026-06-22 | P2-6 | - | `IndexBuilder` `Skip(i).Take(batch)` → `GetRange(i, take)`，O(n²) → O(n) |
| 2026-06-22 | P2-7~P2-8 | - | App 启动后台预热 RAG 索引 + `ReloadEmbeddingProvider` 异常路由到 `SettingsViewModel`；Release 构建通过 |
| 2026-06-22 | P2-9 | - | `TrimHistory` 处理 assistant 带 tool_calls 的孤儿组删除 |
| 2026-06-22 | P2-10 | - | `AppConfiguration` ApiKey 用 DPAPI 加密存储（`ProtectedData`），新增 NuGet 包 |
| 2026-06-22 | P2-11 | - | `Avalonia.Diagnostics` 改为 Debug-only 条件引用 |
| 2026-06-22 | P3-2 | - | `Directory.Build.props` 统一 4 属性 + Release `TreatWarningsAsErrors`；8 个 csproj 简化 |
| 2026-06-22 | P3-1 | - | CI 矩阵 3 OS + `dotnet format` check + Release publish artifact |
| 2026-06-22 | P3-4 | - | README 更新环境变量依赖说明 + config.json 改为 `EncryptedApiKey` |
| 2026-06-22 | P3-3 | - | ViewModel 测试：`Avalonia.Headless` + `ProjectViewModelTests`(6) + `ChatMessageViewModelTests`(10)；+16 测试，130 全过 |
| 2026-06-22 | P3-5 | - | `IAgentSessionFactory` + `AgentSession` Transient + `ChatViewModel` IDisposable 正确取消订阅 |
| 2026-06-22 | P3-6 | - | `DiagnoseLuaTool` `StripStringsAndComments` + 转义感知引号检测 + 精确 debug 7 匹配；+9 误报减少测试，139 全过 |

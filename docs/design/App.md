# Design Doc: App

> **关联 ADR**：[ADR-003](../adr/ADR-003-windows-only-avalonia-desktop.md)、[ADR-005](../adr/ADR-005-headless-unit-test-session.md)

## 概述

`IsaacAgent.App` 是 Avalonia 11 桌面壳：MVVM、DI、设置、聊天 UI、项目文件树。

## 设计目标

- Cursor 式多 Tab 聊天 + 项目侧栏
- 设置持久化（`AppConfiguration` + DPAPI 加密 API Key）
- 可测试的 ViewModel（Headless Avalonia）

## 实现概览

### 架构

- **DI**：`App.ConfigureServices()` 注册单例/瞬态服务
- **ViewModels**：`CommunityToolkit.Mvvm` + `[RelayCommand]`
- **Views**：`x:DataType` 编译绑定

### 关键 ViewModel

| ViewModel | 职责 |
|-----------|------|
| `ChatTabViewModel` | 单 Tab 聊天；持有 `AgentSession`；切换项目 Dispose |
| `ProjectViewModel` | 文件树；`RefreshFilesAsync` UI 线程 marshal |
| `SettingsViewModel` | 配置编辑、索引状态；Save 经注入的 **Settings apply** |

### Settings apply

| 类型 | 职责 |
|------|------|
| `ISettingsApply` / `SettingsApply` | 消费 **provider intent**：立即换 chat provider；仅当 embedding 相关字段变化时后台启动 **Embedding apply** |
| `IEmbeddingApply` / `EmbeddingApplyAdapter` | App 侧对 Rag `EmbeddingApply` 的可注入缝 |
| `ISettingsApplyProgress` / `SettingsApplyProgress` | 进度/结果回调 → Settings 重建标志、状态文案、toast（apply 不 service-locate ViewModel） |
| `ProviderIntent` | 内存中的 LLM + embedding 快照（非再次 `AppConfiguration.Load`） |

不变量：

- Save 持久化后调用 Settings apply；**不再**使用静态 `App.ReloadLlmProvider` / `App.ReloadEmbeddingProvider`
- LLM-only 变更跳过 Embedding apply；embedding 变更 fire-and-forget 重建，Save 不等待完成
- 再次需要重建的 Save 取消上一次 in-flight rebuild（`CancellationToken`，并与 shutdown token 链接）
- Language / theme / accent / font 仍走既有 Theme / Localization / `FontSizeService` 路径（不属于 Settings apply）

### 设置与安全

- **API Key**：内存明文 + DPAPI 持久化；`ApiKey` 带 `[JsonIgnore]`，磁盘仅写 `EncryptedApiKey`
- **拖放**：文件夹打开为项目；文件注入聊天上下文，单文件上限 256 KB（与 Before-image 单文件上限对齐，见 [Agent.md](Agent.md) Checkpoint 合同）
- **发布校验**：`IsaacAgent.exe --verify-onnx` 无 UI 校验捆绑 ONNX 可加载（供 Nuke `PublishVerify`）

### Chat session store

术语见 `CONTEXT.md`。App 拥有项目级聊天持久化缝 `IChatSessionStore` / `FileChatSessionStore`：

| 面 | 合同 |
|----|------|
| **布局** | `%APPDATA%/IsaacAgent/sessions/{projectHash}.json`（可注入 root 便于测试）；`projectHash` = `SHA256(UTF8(path.ToLowerInvariant()))` 前 12 hex（与旧 `history/` 一致） |
| **载荷** | 有序 tabs：稳定 `Guid`、title、Agent 形 envelope（`HistoryVersion` + `List<ChatMessage>`）；UI 气泡为投影，不在本缝内 |
| **Save 结果** | `SaveAsync` 返回 `bool`：写出成功为 `true`；无项目 / 写失败为 `false`（打日志，不抛） |
| **无项目** | `projectDir` 空 / 空白 → 不读不写 |
| **软失败** | 缺失或损坏文件 → 打日志 + 空 manifest，不抛给调用方 |
| **不持久化** | Checkpoint、Before-image、tip hash（保持会话内短暂） |
| **一次性迁移** | `sessions/` 缺失时，在进程内闸门下从 legacy `history/`（消息内容优先；多文件按 LastWriteTime 与 `chat-history/` 顺序按索引对齐）与 `chat-history/`（title/顺序，若有）构建 manifest，并**始终**写入 `sessions/`（含空 manifest）；写失败则软失败为空会话（不返回未落盘迁移结果）；legacy 文件保留不动；之后不再以 legacy 为权威 |
| **可注入根** | 生产默认 `%APPDATA%/IsaacAgent/{sessions,history,chat-history}`；测试可注入三根目录 |

ViewModel 接线以及 `ChatHistoryService.SaveSession`/`RestoreSession` 退役不在本缝落地范围内（后续 ticket）。

### Checkpoint / Restore UX

术语见 `CONTEXT.md`。核心语义与文件回滚在 [Agent.md](Agent.md)；App 负责发现入口、确认、设置与输入框回填。

| 面 | 合同 |
|----|------|
| **入口** | 对应用户消息气泡旁的 **Restore** 控件（该消息自动 Checkpoint 仍存活时显示）；无独立时间线要求 |
| **确认框必述** | ① 从该用户回合起截断对话；② 按 Before-image 回滚 Tracked write（适用当前 Hand-edit conflict mode）；③ 若有进行中生成则取消；④ 该条提示词回填输入框；⑤ **`run_command` / 未跟踪副作用不撤销**。确认 / 取消。具体文案与多语言为实现细节 |
| **完成后** | 取消进行中回合（如有）；对话与文件侧按 Agent 语义完成；提示词回填当前 Tab 输入框 |
| **Hand-edit conflict mode** | Settings 薄 **Agent** 分区：一项 `force`（默认）/ `skip`，经 `AppConfiguration.HandEditConflictMode` 持久化；Save 走既有设置持久化路径（非 LLM/embedding Settings apply）。聊天 Restore 在确认后读取该配置传入 `AgentSession.RestoreAsync` |
| **命名** | UI 使用 **Restore**（Checkpoint）。勿与 `ChatHistoryService.RestoreSession`（会话反序列化）混称 |

不在 App 合同内：Redo、Edit-previous、跨重启 Checkpoint UI、Git 级 rewind。

**实现要点（chat Restore）**：`ChatTabViewModel.RestoreCommand` → `IRestoreConfirmDialog` → 必要时取消 in-flight → `AgentSession.RestoreAsync(..., AppConfiguration.HandEditConflictMode)` → 截断 UI 消息 → `InputText` 回填。`ChatMessageViewModel.CanRestore` / `CheckpointId` 与 live `AgentSession.Checkpoints` 对齐。

### 测试策略（ADR-005）

- `[assembly: AvaloniaTestApplication]` + `HeadlessTestApp`
- Avalonia 测试用 `[AvaloniaFact]`，非 `[Fact]`
- `AvaloniaTestHelper.FlushDispatcher()` 委托 `HeadlessUnitTestSession`
- Settings apply 缝：`SettingsApplyTests`（fake Embedding apply / chat 工厂）；Save 路径：`SettingsViewModelTests`（fake `ISettingsApply`）
- Checkpoint Restore UX 缝：`ChatTabViewModelTests`（fake `IRestoreConfirmDialog` + Scripted/Gate chat）

## 设计权衡

- **Windows-only**：简化 DPAPI 与发布；见 ADR-003。
- **Markdown 自绘**：`MarkdownRenderer` 适配 Avalonia 能力（无 WPF `Run.Underline`）。
- **Settings apply 薄模块**：chat 换源 + 条件触发 Embedding apply；重建深度留在 Rag。
- **Restore 入口贴用户消息**：与「每条用户消息一个 Checkpoint」同位，对齐 VS Code Chat 发现路径。

## 已知局限

- 无 macOS / Linux 官方构建或库层跨平台 CI（严格 Windows-only；见 ADR-003）
- Toast 自动消失依赖 `TestDismissScheduler` 测试 hook
- 启动预热失败仍可能经 `App.Services` 更新 Settings 状态（非 Save 路径）
- Hand-edit conflict mode：Settings Agent 区已落地；聊天 Restore 经 `AppConfiguration.HandEditConflictMode` 消费（默认可注入 `Func<HandEditConflictMode>` 便于测试）

## 参考

- `src/IsaacAgent.App/`
- `CONTEXT.md`（Settings apply / provider intent / Checkpoints 术语）
- [Agent.md](Agent.md)（Checkpoint 核心合同）
- `tests/IsaacAgent.Tests/AvaloniaTestHelper.cs`
- `tests/IsaacAgent.Tests/SettingsApplyTests.cs`

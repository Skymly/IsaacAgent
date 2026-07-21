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
- **拖放**：文件夹打开为项目；文件注入聊天上下文，单文件上限 256 KB
- **发布校验**：`IsaacAgent.exe --verify-onnx` 无 UI 校验捆绑 ONNX 可加载（供 Nuke `PublishVerify`）

### 测试策略（ADR-005）

- `[assembly: AvaloniaTestApplication]` + `HeadlessTestApp`
- Avalonia 测试用 `[AvaloniaFact]`，非 `[Fact]`
- `AvaloniaTestHelper.FlushDispatcher()` 委托 `HeadlessUnitTestSession`
- Settings apply 缝：`SettingsApplyTests`（fake Embedding apply / chat 工厂）；Save 路径：`SettingsViewModelTests`（fake `ISettingsApply`）

## 设计权衡

- **Windows-only**：简化 DPAPI 与发布；见 ADR-003。
- **Markdown 自绘**：`MarkdownRenderer` 适配 Avalonia 能力（无 WPF `Run.Underline`）。
- **Settings apply 薄模块**：chat 换源 + 条件触发 Embedding apply；重建深度留在 Rag。

## 已知局限

- 无 macOS / Linux 官方构建或库层跨平台 CI（严格 Windows-only；见 ADR-003）
- Toast 自动消失依赖 `TestDismissScheduler` 测试 hook
- 启动预热失败仍可能经 `App.Services` 更新 Settings 状态（非 Save 路径）

## 参考

- `src/IsaacAgent.App/`
- `CONTEXT.md`（Settings apply / provider intent 术语）
- `tests/IsaacAgent.Tests/AvaloniaTestHelper.cs`
- `tests/IsaacAgent.Tests/SettingsApplyTests.cs`

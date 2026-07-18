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
| `SettingsViewModel` | 配置编辑、索引状态、嵌入热切换 |

### 设置与安全

- **API Key**：内存明文 + DPAPI 持久化；`ApiKey` 带 `[JsonIgnore]`，磁盘仅写 `EncryptedApiKey`
- **拖放**：文件夹打开为项目；文件注入聊天上下文，单文件上限 256 KB
- **发布校验**：`IsaacAgent.exe --verify-onnx` 无 UI 校验捆绑 ONNX 可加载（供 Nuke `PublishVerify`）

### 测试策略（ADR-005）

- `[assembly: AvaloniaTestApplication]` + `HeadlessTestApp`
- Avalonia 测试用 `[AvaloniaFact]`，非 `[Fact]`
- `AvaloniaTestHelper.FlushDispatcher()` 委托 `HeadlessUnitTestSession`

## 设计权衡

- **Windows-only**：简化 DPAPI 与发布；见 ADR-003。
- **Markdown 自绘**：`MarkdownRenderer` 适配 Avalonia 能力（无 WPF `Run.Underline`）。

## 已知局限

- 无 macOS / Linux 官方构建或库层跨平台 CI（严格 Windows-only；见 ADR-003）
- Toast 自动消失依赖 `TestDismissScheduler` 测试 hook

## 参考

- `src/IsaacAgent.App/`
- `tests/IsaacAgent.Tests/AvaloniaTestHelper.cs`

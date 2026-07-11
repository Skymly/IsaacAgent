# ADR-005: HeadlessUnitTestSession for Avalonia tests

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-07-08 |
| **关联 Issue** | 无 — 测试稳定性整改 |

## 背景

Avalonia Headless 测试中手动 `SetupWithoutStarting()` + `Dispatcher.UIThread.Invoke()` 导致全量测试套件在 `ProjectViewModelTests` 之后死锁；`ObservableCollection` 跨线程更新加剧 Dispatcher 状态污染。

## 决策

- 使用 Avalonia 官方 **`HeadlessUnitTestSession`** + `[assembly: AvaloniaTestApplication]`。
- Avalonia 相关测试使用 **`[AvaloniaFact]`**（`Avalonia.Headless.XUnit`），在 UI 线程上执行并带消息泵。
- `ProjectViewModel.RefreshFilesAsync` 将文件树更新 marshal 到 UI 线程。
- `AvaloniaTestHelper.FlushDispatcher()` 在非 UI 线程使用 `Session.Dispatch()`。

## 后果

- 正面：649 测试稳定通过；与 Avalonia 官方测试实践一致。
- 负面：测试项目增加 `Avalonia.Headless.XUnit` 依赖；Avalonia 测试须用 `[AvaloniaFact]` 而非 `[Fact]`。

## 参考

- [design/App.md](../design/App.md)

# ADR-003: Windows-only Avalonia desktop

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-01 |
| **关联 Issue** | 无 — 项目初始架构 |

## 背景

目标用户主要在 Windows 上开发 Isaac Mod；Avalonia 虽跨平台，但 DPAPI 凭据加密、发布流水线与 UI 验证均按 Windows 优化。

## 决策

- 官方支持范围限定为 **Windows x64**；不承诺、维护或持续验证 macOS / Linux 桌面应用及库层兼容性。
- `IsaacAgent.App` 标记 `SupportedOSPlatform: windows`。
- CI 构建、测试与发布仅在 **windows-latest**；不维护跨平台库层 CI（无 `ci-lib` / `CiLib`）。
- 唯一官方发布产物为 `win-x64` 自包含单文件 exe。
- DPAPI（CurrentUser）、Windows 路径约定、Isaac `log.txt` 默认路径与发布验证均以 Windows 为边界。

## 后果

- 正面：发布与测试矩阵简单；与 DPAPI、Windows 路径习惯一致；文档与 CI 表述无歧义。
- 负面：macOS / Linux 用户无法获得官方构建或兼容性保障。

## 参考

- [.github/workflows/build-and-test.yml](../../.github/workflows/build-and-test.yml)

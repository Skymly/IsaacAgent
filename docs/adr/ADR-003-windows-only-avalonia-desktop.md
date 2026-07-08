# ADR-003: Windows-only Avalonia desktop

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-01 |
| **关联 RFC** | 无 — 项目初始架构 |

## 背景

目标用户主要在 Windows 上开发 Isaac Mod；Avalonia 虽跨平台，但 DPAPI 凭据加密、发布流水线与 UI 验证均按 Windows 优化。

## 决策

- `IsaacAgent.App` 标记 `SupportedOSPlatform: windows`。
- CI 全量构建与发布仅在 **windows-latest**；跨平台 `ci-lib` 暂时禁用。
- 发布产物为 `win-x64` 单文件自包含 exe。

## 后果

- 正面：发布与测试矩阵简单；与 DPAPI、Windows 路径习惯一致。
- 负面：macOS/Linux 用户无法获得官方构建；库层理论上可跨平台但无 CI 保障。

## 参考

- [.github/workflows/build-and-test.yml](../../.github/workflows/build-and-test.yml)

# Design Doc: Core

> **关联 ADR**：[ADR-001](../adr/ADR-001-multi-project-layered-architecture.md)
> **关联 Issue**：#59

## 概述

`IsaacAgent.Core` 存放跨模块共享的领域模型、服务契约，以及不依赖 Avalonia / 工具 schema 的纯策略类型。本文件记录其中需要维护者约定的深模块；契约接口（`ITool` / `ISkill` / `IChatService` 等）仍以各消费方 Design Doc 为主。

## 设计目标

- 共享策略只在 Core 实现一次，避免 Tools / Agent / Rag / App 各自维护副本
- 保持 Core 无 UI、无工具 JSON schema、无 I/O 副作用（路径策略可查环境与规范化路径，不读写文件内容）

## API 面

### `ProjectPathSafety`（`IsaacAgent.Core.PathSafety`）

项目沙箱路径策略的单一实现（具体静态类型，非 `interface`）。

| 成员 | 职责 |
|------|------|
| `Resolve(projectDir, relPath)` | 规范化相对路径（含 3+ 点折叠）后 `GetFullPath`，返回 `(FullPath, IsSafe)` |
| `IsWithinProject(fullPath, projectDir)` | 根目录尾部分隔符，防 sibling-prefix 穿越 |
| `GetDefaultIsaacLogPath()` | Documents/My Games/Binding of Isaac Repentance/log.txt（不检查存在） |
| `IsAllowedAbsoluteLogPath(path)` | 绝对路径白名单：仅当等于默认 Isaac log（忽略大小写） |

消费方切换（删除本地副本）见 follow-ups #60–#63；本模块 PR 只发布 API + 单测。

## 不变量

1. **Sibling-prefix 安全**：`IsWithinProject` 必须在比较前为项目根追加 `Path.DirectorySeparatorChar`（或显式允许路径等于根本身）。
2. **相对路径穿越**：`Resolve` 须折叠 `....` 类双编码段，再交给 `Path.GetFullPath`。
3. **Log 绝对路径**：除默认 Isaac `log.txt` 外，不得放行任意绝对路径；相对路径走 `Resolve` / `IsWithinProject`。
4. **无第二适配器则不加 interface**：路径策略以具体深模块发布。

## 实现概览

逻辑对齐历史 `FileToolPathSafety`（Tools）与 `ProjectPathSafety`（Agent）及 `ParseLogTool` 的绝对路径白名单意图；Agent 侧的 `ToRelativeKey` 仍属 Checkpoint 关注点，不纳入本表面。

## 设计权衡

- **静态深模块 vs instance**：调用方已习惯 `(projectDir, path)` 参数形式；静态 API 便于 #60–#63 逐点替换，且无生命周期需求。
- **白名单与文件是否存在解耦**：`GetDefaultIsaacLogPath` 始终返回规范路径；存在性由调用方（如 `parse_log`）自行判断。

## 已知局限

- 尚未替换 Tools / Agent / Rag / App 内联副本（#60–#63）。
- Windows 路径比较使用 `OrdinalIgnoreCase`，与现网工具行为一致。

## 参考

- [Tools.md](Tools.md) 路径安全不变量
- Issue #58（父）、#59（本提取）

# 开发手册

> 编码规范与模块边界见 [AGENTS.md](../AGENTS.md)；贡献与 PR 流程见 [CONTRIBUTING.md](../CONTRIBUTING.md)；文档约定见 [DOCUMENTATION.md](DOCUMENTATION.md)。

## 环境要求

- **.NET 8 SDK**
- **Windows x64** — 唯一官方支持平台（`SupportedOSPlatform: windows`；见 [ADR-003](adr/ADR-003-windows-only-avalonia-desktop.md)）。不维护 macOS / Linux 桌面或库层跨平台 CI。
- **Git** — 完整历史（MinVer 从 tag 推导版本）

## 克隆与构建

```powershell
git clone https://github.com/Skymly/IsaacAgent.git
cd IsaacAgent
dotnet build IsaacAgent.sln -c Release
```

## 运行

```powershell
dotnet run --project src/IsaacAgent.App/IsaacAgent.App.csproj -c Release
```

## 测试

```powershell
# CI 等价（format + build + 单元/Headless 测试；不含 FlaUI）
./build.ps1 --target CiAll --configuration Release

# 仅单元 / Headless 测试项目
dotnet test tests/IsaacAgent.Tests/IsaacAgent.Tests.csproj -c Release
```

### Avalonia Headless 测试

- 使用 `[AvaloniaFact]`（非 `[Fact]`）
- 程序集属性：`[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]`
- 详见 [design/App.md](design/App.md)、[ADR-005](adr/ADR-005-headless-unit-test-session.md)

### FlaUI UI 测试（Nightly / 本地手动）

真窗自动化在独立项目 `tests/IsaacAgent.UiTests`（FlaUI + xUnit），**不**纳入 `Ci` / `CiAll` / `Release`。

| Nuke 目标 | 被测产物 | 范围 |
|-----------|----------|------|
| `UiTest` | App `bin/Release`（非 Publish） | 全套 UiTests（含 Chat / Settings smoke） |
| `UiTestPublish` | `artifacts/publish/win-x64/IsaacAgent.exe`（依赖 `Publish`，非 `PublishVerify`） | `--filter FlaUI=PublishSmoke`（冷启动 + `--project` A→B） |

```powershell
# 构建输出全套（Nightly 第一步）
./build.ps1 --target UiTest --configuration Release

# Publish 制品冒烟（Nightly 第二步；设置 ISAACAGENT_APP_EXE）
./build.ps1 --target UiTestPublish --configuration Release --runtime win-x64
```

- GitHub Actions：`.github/workflows/ui-tests.yml`（`schedule` + `workflow_dispatch`，`windows-latest`；同 job 先 `UiTest` 再 `UiTestPublish`）
- 可选覆盖被测 exe：环境变量 `ISAACAGENT_APP_EXE`（`UiTestPublish` 会指向 Publish 产物）
- AutomationId / `--project` / `FlaUI=PublishSmoke` 契约见 [design/App.md](design/App.md)
- Windows runner 的 GUI 会话偶发不稳定时，优先用 `workflow_dispatch` 或本地 `UiTest` / `UiTestPublish` 验证；勿为此把 FlaUI 绑进 PR CI

## Nuke 目标

| 目标 | 说明 |
|------|------|
| `Ci` | Clean → Restore → Compile → UnitTest |
| `CiAll` | Format + Ci |
| `UiTest` | 构建 App（Release）并运行 `IsaacAgent.UiTests` 全套（非 Ci/CiAll 依赖） |
| `UiTestPublish` | `Publish` 后对 Publish exe 跑 `FlaUI=PublishSmoke`（非 Ci/CiAll/Release 依赖） |
| `Format` / `FormatFix` | 格式化检查 / 修复 |
| `Publish` | 自包含 win-x64 exe → `artifacts/publish/` |
| `PublishVerify` | Publish + EXE 体积 / 旁路 ONNX / EXE-only `--verify-onnx` |
| `Release` | CiAll + PublishVerify |

## 解决方案布局

```
src/
  IsaacAgent.App/       Avalonia UI
  IsaacAgent.Core/      领域模型、接口
  IsaacAgent.LLM/       LLM 提供商
  IsaacAgent.Tools/     文件/项目/知识工具
  IsaacAgent.Agent/     会话编排、Skill
  IsaacAgent.Rag/       RAG 管线 + 嵌入知识
tests/
  IsaacAgent.Tests/     单元 / Avalonia Headless
  IsaacAgent.UiTests/   FlaUI 真窗（Nightly / Nuke UiTest + UiTestPublish）
build/                  Nuke 脚本
docs/                   维护者文档（本体系）
```

## 配置

- 用户配置：`%APPDATA%/IsaacAgent/config.json`
- API Key 经 DPAPI 加密存储
- 嵌入 provider：`onnx`（默认，捆绑 all-MiniLM-L6-v2；首次构建会下载 `Resources/onnx/model.onnx`）或 `ollama`

## 文档更新（摘要）

- 工具 / Skill / API / 实现变更 → 更新对应 Design Doc
- 破坏性架构决策 → 新建 ADR + Design Doc
- 用户可见行为 → CHANGELOG `[Unreleased]`
- 任务追踪 → GitHub Issues

## 格式化

```powershell
./build.ps1 --target FormatFix
```

CI 的 `Format` 目标会在未格式化时失败。

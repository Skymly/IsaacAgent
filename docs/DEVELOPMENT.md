# 开发手册

> 编码规范与模块边界见 [AGENTS.md](../AGENTS.md)；贡献与 PR 流程见 [CONTRIBUTING.md](../CONTRIBUTING.md)；文档驱动流程见 [DOCUMENTATION.md](DOCUMENTATION.md)。

## 环境要求

- **.NET 8 SDK**
- **Windows** — App 目标平台（`SupportedOSPlatform: windows`）
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
# CI 等价（format + build + 全量测试）
./build.ps1 --target CiAll --configuration Release

# 仅测试
dotnet test IsaacAgent.sln -c Release
```

### Avalonia Headless 测试

- 使用 `[AvaloniaFact]`（非 `[Fact]`）
- 程序集属性：`[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]`
- 详见 [design/App.md](design/App.md)、[ADR-005](adr/ADR-005-headless-unit-test-session.md)

## Nuke 目标

| 目标 | 说明 |
|------|------|
| `Ci` | Clean → Restore → Compile → UnitTest |
| `CiLib` | 仅库项目（跨平台 CI） |
| `CiAll` | Format + Ci |
| `Format` / `FormatFix` | 格式化检查 / 修复 |
| `Publish` | 自包含 exe → `artifacts/publish/` |
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
  IsaacAgent.Tests/
build/                  Nuke 脚本
docs/                   维护者文档（本体系）
```

## 配置

- 用户配置：`%APPDATA%/IsaacAgent/config.json`
- API Key 经 DPAPI 加密存储
- 嵌入 provider：`onnx`（默认）或 `ollama`

## 文档驱动开发（摘要）

1. 非琐碎变更先查 [DOCUMENTATION.md §11](DOCUMENTATION.md#11-文档驱动开发流程) 决策表
2. 新工具 / Skill → RFC + ADR + Spec
3. 实现 PR 同步更新 Design Doc + CHANGELOG
4. 大型任务先写 `docs/plans/`

## 格式化

```powershell
./build.ps1 --target FormatFix
```

CI 的 `Format` 目标会在未格式化时失败。

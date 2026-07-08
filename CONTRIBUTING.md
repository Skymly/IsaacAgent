# Contributing to IsaacAgent

Thanks for your interest in contributing! This guide covers setup,
conventions, and the pull request process.

For the full **documentation-driven development** workflow, see
[docs/DOCUMENTATION.md](docs/DOCUMENTATION.md). Development setup
details are in [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Development Setup

### Prerequisites

- **.NET 8 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Windows** — the Avalonia UI targets `windows` as its supported OS platform
- **Git** — with full history (needed by MinVer for versioning)

### Clone & Build

```powershell
git clone https://github.com/Skymly/IsaacAgent.git
cd IsaacAgent
dotnet build IsaacAgent.sln -c Release
```

### Run the App

```powershell
dotnet run --project src/IsaacAgent.App/IsaacAgent.App.csproj -c Release
```

### Run Tests

```powershell
# Via Nuke (CI-equivalent: format check + build + test)
./build.ps1 --target CiAll --configuration Release

# Or just tests
dotnet test IsaacAgent.sln
```

### Nuke Build Targets

| Target | Description |
|--------|-------------|
| `Ci` | Clean → Restore → Compile → UnitTest |
| `CiLib` | Library-only CI (for Linux/macOS) |
| `Format` | `dotnet format --verify-no-changes` |
| `FormatFix` | `dotnet format` (applies fixes) |
| `CiAll` | Format + Ci (full verification) |

## Project Structure

```
src/
  IsaacAgent.App/       Avalonia UI (Views, ViewModels, DI)
  IsaacAgent.Core/      Domain models, interfaces, knowledge base
  IsaacAgent.LLM/       LLM provider abstraction (OpenAI-compatible, Ollama)
  IsaacAgent.Tools/     Agent tools (file ops, scaffolding, diagnostics, API search)
  IsaacAgent.Rag/       RAG pipeline (chunking, embedding, retrieval, XML validation)
  IsaacAgent.Agent/     Agent engine (orchestration, tool routing, prompts)
tests/
  IsaacAgent.Tests/     Unit and integration tests
build/                  Nuke build script
```

## Code Conventions

### C# Style

- **C# 12** features allowed
- **File-scoped namespaces** (`namespace Foo;` not `namespace Foo { ... }`)
- **Nullable reference types** enabled — honor nullability annotations
- **`async/await`** for all I/O; avoid `.Result` / `.Wait()`
- **Expression-bodied members** for short methods
- **`var`** for local variables when the type is obvious
- **No AI tool/model names** in commits, code comments, or docs

### MVVM Pattern

- **ViewModels** inherit `ObservableObject` (CommunityToolkit.Mvvm)
- **Commands** use `[RelayCommand]` source generators
- **Views** use `x:DataType` for compiled bindings
- **DI** — all services registered in `App.ConfigureServices()`

### Testing

- Write unit tests for new tools and services
- Test files go in `tests/IsaacAgent.Tests/`
- Follow the existing naming convention: `ClassNameTests.cs`
- Use xUnit (`[Fact]` for single cases, `[Theory]` for parameterized)
- Avalonia UI tests use `[AvaloniaFact]` (see `docs/design/App.md`)
- Aim for meaningful coverage of edge cases, not just happy paths

### Formatting

The project uses `dotnet format` with the `.editorconfig` settings. Run
before committing:

```powershell
./build.ps1 --target FormatFix
```

CI will fail if formatting is not applied (`Format` target).

## Documentation-Driven Changes

Before implementing non-trivial features:

1. Check [docs/DOCUMENTATION.md §11](docs/DOCUMENTATION.md#11-文档驱动开发流程) for required docs (RFC, ADR, Spec, Plan).
2. New **tools** or **skills** require RFC + ADR + Spec updates.
3. Implementation PRs must sync **Design Doc** and **CHANGELOG** `[Unreleased]`.
4. Large tasks: create a plan in `docs/plans/` and a tracking Issue.

## Pull Request Process

1. **Fork** the repository and create a feature branch:
   ```powershell
   git checkout -b feature/my-feature
   ```

2. **Write tests** for your changes (if applicable).

3. **Run full verification** before pushing:
   ```powershell
   ./build.ps1 --target CiAll --configuration Release
   ```
   This checks formatting, builds, and runs all tests.

4. **Commit with clear messages**:
   - Use English, imperative mood ("Add feature X", not "Added feature X")
   - Reference issues if applicable ("Fix #123: ...")
   - No AI tool attribution in commit messages

5. **Push and open a PR** against `main`:
   ```powershell
   git push origin feature/my-feature
   gh pr create --title "Add feature X" --body "Description..."
   ```

6. **Ensure CI passes** — Windows full suite (`CiAll`); library tests
   also run on Linux/macOS via `CiLib`.

7. **Address review feedback** — push fixes to the same branch.

## Reporting Issues

- Use the [Issue templates](.github/ISSUE_TEMPLATE/) for bug reports and
  feature requests.
- Include your OS, .NET version, and steps to reproduce.
- For bugs, attach the relevant `log.txt` output if applicable.

## License

By contributing, you agree that your contributions will be licensed under
the [MIT License](LICENSE).

# DocPilot - Agent Instructions

> This file is the source of truth for AI agents. Keep it updated on architectural changes.

## Build & Test

```bash
dotnet build                                    # Build all
dotnet test                                     # Run all tests
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"  # Single test
dotnet test --filter "FullyQualifiedName~ClassName"             # Test class
dotnet run --project src/DocPilot -- --help    # Run CLI
```

## Architecture

```
CLI Commands (analyze/generate/pr)
         ↓
  DocumentationPipeline ─── orchestrates flow
         ↓
  ┌──────┴──────┐
Orchestrator  DocWriter ─── Copilot SDK agents
  Agent        Agent
  │             │
  ↓             ↓
Analysis ───→ Generation ───→ PullRequestCreator
```

### Component Map

| Layer | Location | Purpose |
|-------|----------|---------|
| Commands | `Commands/` | CLI entry points (System.CommandLine) |
| Pipeline | `Pipeline/` | Orchestration: analyze → generate → PR |
| Agents | `Agents/` | Copilot SDK sessions with custom tools |
| Tools | `Agents/Tools/` | AIFunction tools for agents |
| Analysis | `Analysis/` | Git diff (LibGit2Sharp) |
| Heuristics | `Heuristics/` | Change classification, doc target mapping |
| Generation | `Generation/` | Patch creation, file modification |
| GitHub | `GitHub/` | PR creation (Octokit) |

### Agent Pattern

```csharp
await using var agent = new OrchestratorAgent(repoPath, config);
await agent.InitializeAsync();
var result = await agent.AnalyzeAsync(baseRef, headRef);
```

### Tool Definition

```csharp
AIFunctionFactory.Create(
    ([Description("Base ref")] string baseRef,
     [Description("Head ref")] string headRef) => {
        return new DiffAnalyzer(path).AnalyzeRange(baseRef, headRef);
    },
    "analyze_diff",
    "Analyze git diff between refs");
```

## Key Conventions

### Async Disposal
All agents/pipeline implement `IAsyncDisposable`. Always use `await using`.

### Result Pattern
Operations return result objects with `Success` bool and optional `Error` string.

### Configuration
`DocPilotConfig` from `docpilot.yml`. Default: `DocPilotConfig.Default`.

### Testing
- xUnit + FluentAssertions + NSubstitute
- Naming: `Method_Scenario_Expected`
- Use `[Theory]` with `[InlineData]` for parameterized tests

## Git Practices

### Atomic Commits
- One logical change per commit
- Each commit should build and pass tests
- Split large changes into reviewable chunks

### Conventional Commits
```
type(scope): description

feat(analyzer): add TypeScript support
fix(pr): handle repos without main branch
docs(readme): update installation guide
refactor(agents): extract tool factory
test(heuristics): add classifier edge cases
ci(release): add ARM64 builds
```

Types: `feat`, `fix`, `docs`, `refactor`, `test`, `ci`, `chore`

## Maintenance Rules

### Update This File When:
- Adding/removing/renaming components
- Changing architecture or data flow
- Modifying agent or tool patterns
- Adding new conventions

### Update Documentation When:
- Adding new CLI commands or options
- Changing configuration schema
- Modifying installation/setup process
- Adding new features

Documentation files to update:
- `README.md` - Quick start, command reference
- `docs/configuration.md` - Config options
- `docs/architecture.md` - Technical details

## Structured Autonomy

Planning prompts in `.github/prompts/`:
- `/sa-plan` - Create development plans
- `/sa-generate` - Generate implementation docs  
- `/sa-implement` - Execute step-by-step

Plans: `plans/{feature}/plan.md` + `implementation-*.md`

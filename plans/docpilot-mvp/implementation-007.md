# DocPilot MVP - Implementation Guide (Part 7)

## Step 7: CLI Commands Implementation

### Step-by-Step Instructions

#### 7.1 Creare directory Commands e Pipeline
- [x] Esegui:

```powershell
New-Item -ItemType Directory -Path "src/DocPilot/Commands" -Force
New-Item -ItemType Directory -Path "src/DocPilot/Pipeline" -Force
```

#### 7.2 Creare DocumentationPipeline.cs
- [x] Crea il file `src/DocPilot/Pipeline/DocumentationPipeline.cs`:

```csharp
using System.Text.Json;
using DocPilot.Agents;
using DocPilot.Analysis;
using DocPilot.Configuration;
using DocPilot.Generation;
using DocPilot.Heuristics;

namespace DocPilot.Pipeline;

public sealed class DocumentationPipeline : IAsyncDisposable
{
    private readonly string _repositoryPath;
    private readonly DocPilotConfig _config;
    private OrchestratorAgent? _orchestrator;
    private DocWriterAgent? _writer;
    private bool _disposed;

    public DocumentationPipeline(string repositoryPath, DocPilotConfig config)
    {
        _repositoryPath = repositoryPath;
        _config = config;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _orchestrator = new OrchestratorAgent(_repositoryPath, _config);
        _writer = new DocWriterAgent(_repositoryPath, _config);

        await _orchestrator.InitializeAsync(cancellationToken);
        await _writer.InitializeAsync(cancellationToken);
    }

    public async Task<PipelineResult> RunAnalyzeAsync(
        string? baseRef,
        string? headRef,
        bool staged,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_orchestrator is null)
        {
            throw new InvalidOperationException("Pipeline not initialized.");
        }

        OrchestratorResult result;

        if (staged)
        {
            result = await _orchestrator.AnalyzeStagedAsync(cancellationToken);
        }
        else
        {
            var @base = baseRef ?? "HEAD~1";
            var head = headRef ?? "HEAD";
            result = await _orchestrator.AnalyzeAsync(@base, head, cancellationToken);
        }

        return new PipelineResult
        {
            Stage = PipelineStage.Analysis,
            Success = result.Success,
            Diff = result.Diff,
            Mapping = result.Mapping,
            RawOutput = result.RawResponse
        };
    }

    public async Task<PipelineResult> RunGenerateAsync(
        MappingResult mapping,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_writer is null)
        {
            throw new InvalidOperationException("Pipeline not initialized.");
        }

        var writerResult = await _writer.GenerateAsync(mapping, cancellationToken);

        if (!writerResult.Success || writerResult.GeneratedFiles.Count == 0)
        {
            return new PipelineResult
            {
                Stage = PipelineStage.Generation,
                Success = false,
                Error = writerResult.Error ?? "No patches generated"
            };
        }

        // Apply patches if not dry-run
        var applier = new PatchApplier(_repositoryPath);
        var patches = writerResult.GeneratedFiles.Select(f => new DocPatch
        {
            FilePath = f,
            Operation = PatchOperation.Update,
            Content = "" // Content is managed by the agent
        }).ToList();

        return new PipelineResult
        {
            Stage = PipelineStage.Generation,
            Success = true,
            GeneratedFiles = writerResult.GeneratedFiles,
            DryRun = dryRun,
            RawOutput = writerResult.RawResponse
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        if (_orchestrator is not null)
        {
            await _orchestrator.DisposeAsync();
        }

        if (_writer is not null)
        {
            await _writer.DisposeAsync();
        }

        _disposed = true;
    }
}

public enum PipelineStage
{
    Analysis,
    Generation,
    PullRequest
}

public sealed class PipelineResult
{
    public required PipelineStage Stage { get; init; }
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public DiffResult? Diff { get; init; }
    public MappingResult? Mapping { get; init; }
    public List<string>? GeneratedFiles { get; init; }
    public bool DryRun { get; init; }
    public string? RawOutput { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });
}
```

#### 7.3 Creare AnalyzeCommand.cs
- [x] Crea il file `src/DocPilot/Commands/AnalyzeCommand.cs`:

```csharp
using System.CommandLine;
using System.Text.Json;
using DocPilot.Configuration;
using DocPilot.Pipeline;

namespace DocPilot.Commands;

public static class AnalyzeCommand
{
    public static Command Create()
    {
        var baseOption = new Option<string?>(
            aliases: ["--base", "-b"],
            description: "Base commit/branch for diff comparison");

        var headOption = new Option<string?>(
            aliases: ["--head", "-h"],
            description: "Head commit/branch for diff comparison");

        var stagedOption = new Option<bool>(
            aliases: ["--staged", "-s"],
            description: "Analyze staged changes only");

        var outputOption = new Option<string>(
            aliases: ["--output", "-o"],
            getDefaultValue: () => "json",
            description: "Output format (json, text)");

        var configOption = new Option<string?>(
            aliases: ["--config", "-c"],
            description: "Path to docpilot.yml configuration file");

        var command = new Command("analyze", "Analyze code changes and identify documentation targets")
        {
            baseOption,
            headOption,
            stagedOption,
            outputOption,
            configOption
        };

        command.SetHandler(async (context) =>
        {
            var baseRef = context.ParseResult.GetValueForOption(baseOption);
            var headRef = context.ParseResult.GetValueForOption(headOption);
            var staged = context.ParseResult.GetValueForOption(stagedOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var configPath = context.ParseResult.GetValueForOption(configOption);
            var cancellationToken = context.GetCancellationToken();

            var loader = new ConfigurationLoader();
            var config = await loader.LoadAsync(configPath, cancellationToken);

            var repoPath = Directory.GetCurrentDirectory();

            await using var pipeline = new DocumentationPipeline(repoPath, config);
            await pipeline.InitializeAsync(cancellationToken);

            var result = await pipeline.RunAnalyzeAsync(baseRef, headRef, staged, cancellationToken);

            if (output == "json")
            {
                Console.WriteLine(result.ToJson());
            }
            else
            {
                PrintTextOutput(result);
            }

            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static void PrintTextOutput(PipelineResult result)
    {
        Console.WriteLine($"\n=== DocPilot Analysis ===\n");

        if (result.Diff is not null)
        {
            Console.WriteLine($"Files changed: {result.Diff.TotalFilesChanged}");
            Console.WriteLine($"Lines added: +{result.Diff.TotalLinesAdded}");
            Console.WriteLine($"Lines deleted: -{result.Diff.TotalLinesDeleted}");
            Console.WriteLine();
        }

        if (result.Mapping is not null)
        {
            Console.WriteLine($"Change type: {result.Mapping.OverallChangeType}");
            Console.WriteLine($"Overall confidence: {result.Mapping.OverallConfidence} ({result.Mapping.AverageConfidence:P0})");
            Console.WriteLine();

            Console.WriteLine("Documentation targets:");
            foreach (var target in result.Mapping.Targets)
            {
                Console.WriteLine($"  - {target.FilePath}");
                Console.WriteLine($"    Section: {target.Section ?? "(entire file)"}");
                Console.WriteLine($"    Confidence: {target.Confidence} ({target.ConfidenceScore:P0})");
                Console.WriteLine($"    Sources: {string.Join(", ", target.SourceFiles)}");
                Console.WriteLine();
            }
        }
    }
}
```

#### 7.4 Creare GenerateCommand.cs
- [x] Crea il file `src/DocPilot/Commands/GenerateCommand.cs`:

```csharp
using System.CommandLine;
using DocPilot.Configuration;
using DocPilot.Pipeline;

namespace DocPilot.Commands;

public static class GenerateCommand
{
    public static Command Create()
    {
        var dryRunOption = new Option<bool>(
            aliases: ["--dry-run", "-n"],
            description: "Preview changes without applying them");

        var targetOption = new Option<string?>(
            aliases: ["--target", "-t"],
            description: "Target directory for generated docs");

        var baseOption = new Option<string?>(
            aliases: ["--base", "-b"],
            description: "Base commit/branch for diff comparison");

        var headOption = new Option<string?>(
            aliases: ["--head", "-h"],
            description: "Head commit/branch for diff comparison");

        var stagedOption = new Option<bool>(
            aliases: ["--staged", "-s"],
            description: "Analyze staged changes only");

        var configOption = new Option<string?>(
            aliases: ["--config", "-c"],
            description: "Path to docpilot.yml configuration file");

        var command = new Command("generate", "Generate documentation patches based on analysis")
        {
            dryRunOption,
            targetOption,
            baseOption,
            headOption,
            stagedOption,
            configOption
        };

        command.SetHandler(async (context) =>
        {
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var target = context.ParseResult.GetValueForOption(targetOption);
            var baseRef = context.ParseResult.GetValueForOption(baseOption);
            var headRef = context.ParseResult.GetValueForOption(headOption);
            var staged = context.ParseResult.GetValueForOption(stagedOption);
            var configPath = context.ParseResult.GetValueForOption(configOption);
            var cancellationToken = context.GetCancellationToken();

            var loader = new ConfigurationLoader();
            var config = await loader.LoadAsync(configPath, cancellationToken);

            var repoPath = target ?? Directory.GetCurrentDirectory();

            await using var pipeline = new DocumentationPipeline(repoPath, config);
            await pipeline.InitializeAsync(cancellationToken);

            // First, analyze
            Console.WriteLine("Analyzing changes...");
            var analysisResult = await pipeline.RunAnalyzeAsync(baseRef, headRef, staged, cancellationToken);

            if (!analysisResult.Success || analysisResult.Mapping is null)
            {
                Console.Error.WriteLine($"Analysis failed: {analysisResult.Error}");
                context.ExitCode = 1;
                return;
            }

            // Then, generate
            Console.WriteLine("\nGenerating documentation...");
            var generateResult = await pipeline.RunGenerateAsync(analysisResult.Mapping, dryRun, cancellationToken);

            if (!generateResult.Success)
            {
                Console.Error.WriteLine($"Generation failed: {generateResult.Error}");
                context.ExitCode = 1;
                return;
            }

            Console.WriteLine($"\n=== Generation Complete ===");
            Console.WriteLine($"Mode: {(dryRun ? "Dry Run" : "Applied")}");
            Console.WriteLine($"Files generated: {generateResult.GeneratedFiles?.Count ?? 0}");

            if (generateResult.GeneratedFiles is not null)
            {
                foreach (var file in generateResult.GeneratedFiles)
                {
                    Console.WriteLine($"  - {file}");
                }
            }

            context.ExitCode = 0;
        });

        return command;
    }
}
```

#### 7.5 Creare PrCommand.cs
- [x] Crea il file `src/DocPilot/Commands/PrCommand.cs`:

```csharp
using System.CommandLine;
using DocPilot.Configuration;

namespace DocPilot.Commands;

public static class PrCommand
{
    public static Command Create()
    {
        var targetBranchOption = new Option<string?>(
            aliases: ["--target-branch", "-t"],
            description: "Target branch for the PR");

        var draftOption = new Option<bool>(
            aliases: ["--draft", "-d"],
            description: "Create PR as draft");

        var titleOption = new Option<string?>(
            aliases: ["--title"],
            description: "PR title (auto-generated if not provided)");

        var configOption = new Option<string?>(
            aliases: ["--config", "-c"],
            description: "Path to docpilot.yml configuration file");

        var command = new Command("pr", "Create a documentation pull request")
        {
            targetBranchOption,
            draftOption,
            titleOption,
            configOption
        };

        command.SetHandler(async (context) =>
        {
            var targetBranch = context.ParseResult.GetValueForOption(targetBranchOption);
            var draft = context.ParseResult.GetValueForOption(draftOption);
            var title = context.ParseResult.GetValueForOption(titleOption);
            var configPath = context.ParseResult.GetValueForOption(configOption);
            var cancellationToken = context.GetCancellationToken();

            var loader = new ConfigurationLoader();
            var config = await loader.LoadAsync(configPath, cancellationToken);

            Console.WriteLine($"Creating PR...");
            Console.WriteLine($"  Target branch: {targetBranch ?? "main"}");
            Console.WriteLine($"  Draft: {draft}");
            Console.WriteLine($"  Title: {title ?? "(auto-generated)"}");

            // TODO: Implement in Step 8 (GitHub PR Integration)
            Console.WriteLine("\n[PR creation will be implemented in Step 8]");

            await Task.CompletedTask;
            context.ExitCode = 0;
        });

        return command;
    }
}
```

#### 7.6 Aggiornare Program.cs
- [x] Sostituisci il contenuto di `src/DocPilot/Program.cs`:

```csharp
using System.CommandLine;
using DocPilot.Commands;

namespace DocPilot;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("DocPilot - Automated documentation PR generator")
        {
            AnalyzeCommand.Create(),
            GenerateCommand.Create(),
            PrCommand.Create()
        };

        return await rootCommand.InvokeAsync(args);
    }
}
```

### Step 7 Verification Checklist
- [x] `dotnet build` compila senza errori
- [x] `dotnet run --project src/DocPilot -- --help` mostra i tre comandi
- [x] `dotnet run --project src/DocPilot -- analyze --help` mostra tutte le opzioni
- [x] `dotnet run --project src/DocPilot -- generate --help` mostra tutte le opzioni

### Step 7 STOP & COMMIT
**STOP & COMMIT:** Fermarsi qui e attendere che l'utente testi, faccia stage e commit.

Messaggio commit suggerito:
```
feat(cli): implement analyze and generate commands

- Add DocumentationPipeline for orchestrating analysis and generation
- Implement AnalyzeCommand with JSON/text output
- Implement GenerateCommand with dry-run support
- Add PrCommand placeholder for Step 8
- Refactor Program.cs to use command classes
```

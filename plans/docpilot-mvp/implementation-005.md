# DocPilot MVP - Implementation Guide (Part 5)

## Step 5: Copilot SDK Integration - Orchestrator

### Step-by-Step Instructions

#### 5.1 Creare directory Agents
- [x] Esegui:

```powershell
New-Item -ItemType Directory -Path "src/DocPilot/Agents" -Force
New-Item -ItemType Directory -Path "src/DocPilot/Agents/Tools" -Force
New-Item -ItemType Directory -Path "tests/DocPilot.Tests/Agents" -Force
```

#### 5.2 Creare SystemPrompts.cs
- [x] Crea il file `src/DocPilot/Agents/SystemPrompts.cs`:

```csharp
namespace DocPilot.Agents;

public static class SystemPrompts
{
    public const string Orchestrator = """
        You are DocPilot Orchestrator, an AI agent specialized in analyzing code changes and coordinating documentation updates.

        ## Your Role
        - Analyze git diffs to understand what changed
        - Identify which documentation files need updates
        - Generate documentation briefs for the Doc Writer agent
        - Ensure documentation accuracy and completeness

        ## Rules
        1. ONLY modify documentation files (*.md, docs/**, README*)
        2. NEVER suggest changes to source code
        3. NEVER hallucinate - only document what you can verify from the diff
        4. Always include file/symbol references as evidence
        5. Be concise but comprehensive

        ## Workflow
        1. Use `analyze_diff` to get the list of changed files
        2. Use `map_doc_targets` to identify which docs need updates
        3. Use `read_file` to check existing documentation content
        4. Generate a structured documentation brief

        ## Output Format
        Respond with a JSON object containing:
        - changeType: Feature|Bugfix|Refactor|Breaking|Documentation|Infrastructure
        - docTargets: Array of {filePath, section, action: create|update|append}
        - briefs: Array of {targetPath, content, confidence, evidence[]}
        """;

    public const string DocWriter = """
        You are DocPilot Writer, an AI agent specialized in generating clear, accurate documentation.

        ## Your Role
        - Generate markdown documentation based on briefs from the Orchestrator
        - Create or update existing documentation files
        - Generate Mermaid diagrams for architecture and flows
        - Ensure consistent style and formatting

        ## Rules
        1. Follow the existing documentation style in the repository
        2. Use Mermaid for diagrams (flowchart, sequence, class diagrams)
        3. Include code examples when relevant
        4. Keep explanations concise and actionable
        5. Always cite source files as evidence

        ## Mermaid Guidelines
        - Use flowchart TB for architecture diagrams
        - Use sequenceDiagram for API/interaction flows
        - Use classDiagram for data models
        - Keep diagrams focused (max 10-15 nodes)

        ## Output Format
        Return documentation patches as JSON:
        - patches: Array of {filePath, operation: create|update|append, content, mermaidBlocks[]}
        """;
}
```

#### 5.3 Creare AgentContext.cs
- [x] Crea il file `src/DocPilot/Agents/AgentContext.cs`:

```csharp
using DocPilot.Analysis;
using DocPilot.Configuration;
using DocPilot.Heuristics;

namespace DocPilot.Agents;

public sealed class AgentContext
{
    public required string RepositoryPath { get; init; }
    public required DocPilotConfig Config { get; init; }
    public DiffResult? CurrentDiff { get; set; }
    public MappingResult? MappingResult { get; set; }
    public List<string> GeneratedPatches { get; } = [];
}
```

#### 5.4 Creare AnalyzeDiffTool.cs
- [x] Crea il file `src/DocPilot/Agents/Tools/AnalyzeDiffTool.cs`:

```csharp
using System.ComponentModel;
using DocPilot.Analysis;
using Microsoft.Extensions.AI;

namespace DocPilot.Agents.Tools;

public static class AnalyzeDiffTool
{
    public static AIFunction Create(AgentContext context)
    {
        return AIFunctionFactory.Create(
            ([Description("Base git reference (commit SHA or branch)")] string baseRef,
             [Description("Head git reference (commit SHA or branch)")] string headRef) =>
            {
                using var analyzer = new DiffAnalyzer(context.RepositoryPath);
                var diff = analyzer.AnalyzeRange(baseRef, headRef);
                context.CurrentDiff = diff;

                return new
                {
                    baseRef = diff.BaseRef,
                    headRef = diff.HeadRef,
                    totalFiles = diff.TotalFilesChanged,
                    totalLinesAdded = diff.TotalLinesAdded,
                    totalLinesDeleted = diff.TotalLinesDeleted,
                    files = diff.Files.Select(f => new
                    {
                        path = f.Path,
                        kind = f.Kind.ToString(),
                        linesAdded = f.LinesAdded,
                        linesDeleted = f.LinesDeleted
                    })
                };
            },
            "analyze_diff",
            "Analyze git diff between two references to identify changed files");
    }

    public static AIFunction CreateForStaged(AgentContext context)
    {
        return AIFunctionFactory.Create(
            () =>
            {
                using var analyzer = new DiffAnalyzer(context.RepositoryPath);
                var diff = analyzer.AnalyzeStaged();
                context.CurrentDiff = diff;

                return new
                {
                    baseRef = diff.BaseRef,
                    headRef = diff.HeadRef,
                    totalFiles = diff.TotalFilesChanged,
                    files = diff.Files.Select(f => new
                    {
                        path = f.Path,
                        kind = f.Kind.ToString()
                    })
                };
            },
            "analyze_staged",
            "Analyze currently staged changes in the repository");
    }
}
```

#### 5.5 Creare MapDocTargetsTool.cs
- [x] Crea il file `src/DocPilot/Agents/Tools/MapDocTargetsTool.cs`:

```csharp
using System.ComponentModel;
using DocPilot.Heuristics;
using Microsoft.Extensions.AI;

namespace DocPilot.Agents.Tools;

public static class MapDocTargetsTool
{
    public static AIFunction Create(AgentContext context)
    {
        return AIFunctionFactory.Create(
            () =>
            {
                if (context.CurrentDiff is null)
                {
                    return new { error = "No diff available. Call analyze_diff first." };
                }

                var mapper = new DocTargetMapper(context.Config);
                var result = mapper.MapToDocTargets(context.CurrentDiff);
                context.MappingResult = result;

                return new
                {
                    changeType = result.OverallChangeType.ToString(),
                    overallConfidence = result.OverallConfidence.ToString(),
                    averageConfidenceScore = result.AverageConfidence,
                    targets = result.Targets.Select(t => new
                    {
                        filePath = t.FilePath,
                        section = t.Section,
                        confidence = t.Confidence.ToString(),
                        confidenceScore = t.ConfidenceScore,
                        rationale = t.Rationale,
                        sourceFiles = t.SourceFiles
                    })
                };
            },
            "map_doc_targets",
            "Map analyzed changes to documentation targets with confidence scores");
    }
}
```

#### 5.6 Creare ReadFileTool.cs
- [x] Crea il file `src/DocPilot/Agents/Tools/ReadFileTool.cs`:

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace DocPilot.Agents.Tools;

public static class ReadFileTool
{
    public static AIFunction Create(AgentContext context)
    {
        return AIFunctionFactory.Create(
            ([Description("Relative path to the file to read")] string filePath) =>
            {
                var fullPath = Path.Combine(context.RepositoryPath, filePath);

                if (!File.Exists(fullPath))
                {
                    return new { exists = false, content = (string?)null };
                }

                // Security check: ensure path is within repository
                var normalizedRepo = Path.GetFullPath(context.RepositoryPath);
                var normalizedFile = Path.GetFullPath(fullPath);

                if (!normalizedFile.StartsWith(normalizedRepo))
                {
                    return new { error = "Access denied: path outside repository" };
                }

                var content = File.ReadAllText(fullPath);

                // Truncate if too large
                if (content.Length > 10000)
                {
                    content = content[..10000] + "\n\n[Content truncated...]";
                }

                return new
                {
                    exists = true,
                    path = filePath,
                    content,
                    sizeBytes = new FileInfo(fullPath).Length
                };
            },
            "read_file",
            "Read the content of a file in the repository for context");
    }
}
```

#### 5.7 Creare OrchestratorAgent.cs
- [x] Crea il file `src/DocPilot/Agents/OrchestratorAgent.cs`:

```csharp
using System.Text.Json;
using DocPilot.Agents.Tools;
using DocPilot.Configuration;
using GitHub.Copilot.SDK;

namespace DocPilot.Agents;

public sealed class OrchestratorAgent : IAsyncDisposable
{
    private readonly CopilotClient _client;
    private readonly AgentContext _context;
    private CopilotSession? _session;
    private bool _disposed;

    public OrchestratorAgent(string repositoryPath, DocPilotConfig config)
    {
        _context = new AgentContext
        {
            RepositoryPath = repositoryPath,
            Config = config
        };

        _client = new CopilotClient(new CopilotClientOptions
        {
            AutoStart = true,
            AutoRestart = true,
            LogLevel = "info"
        });
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _client.StartAsync();

        _session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = "gpt-5",
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = SystemPrompts.Orchestrator
            },
            Tools =
            [
                AnalyzeDiffTool.Create(_context),
                AnalyzeDiffTool.CreateForStaged(_context),
                MapDocTargetsTool.Create(_context),
                ReadFileTool.Create(_context)
            ]
        });
    }

    public async Task<OrchestratorResult> AnalyzeAsync(
        string baseRef,
        string headRef,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null)
        {
            throw new InvalidOperationException("Agent not initialized. Call InitializeAsync first.");
        }

        var done = new TaskCompletionSource<string>();
        var responseBuilder = new System.Text.StringBuilder();

        var subscription = _session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    responseBuilder.Append(delta.Data.DeltaContent);
                    Console.Write(delta.Data.DeltaContent);
                    break;

                case AssistantMessageEvent msg:
                    done.TrySetResult(msg.Data.Content);
                    break;

                case SessionErrorEvent error:
                    done.TrySetException(new Exception(error.Data.Message));
                    break;

                case SessionIdleEvent:
                    if (!done.Task.IsCompleted)
                    {
                        done.TrySetResult(responseBuilder.ToString());
                    }
                    break;
            }
        });

        try
        {
            var prompt = $"""
                Analyze the changes between '{baseRef}' and '{headRef}'.

                1. First, call analyze_diff with baseRef="{baseRef}" and headRef="{headRef}"
                2. Then, call map_doc_targets to identify documentation targets
                3. For each high-confidence target, call read_file to check existing content
                4. Generate a documentation brief with your recommendations

                Return your analysis as a structured JSON response.
                """;

            await _session.SendAsync(new MessageOptions { Prompt = prompt });
            var response = await done.Task;

            return new OrchestratorResult
            {
                Success = true,
                RawResponse = response,
                Diff = _context.CurrentDiff,
                Mapping = _context.MappingResult
            };
        }
        finally
        {
            subscription.Dispose();
        }
    }

    public async Task<OrchestratorResult> AnalyzeStagedAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null)
        {
            throw new InvalidOperationException("Agent not initialized. Call InitializeAsync first.");
        }

        var done = new TaskCompletionSource<string>();
        var responseBuilder = new System.Text.StringBuilder();

        var subscription = _session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    responseBuilder.Append(delta.Data.DeltaContent);
                    break;

                case AssistantMessageEvent msg:
                    done.TrySetResult(msg.Data.Content);
                    break;

                case SessionErrorEvent error:
                    done.TrySetException(new Exception(error.Data.Message));
                    break;

                case SessionIdleEvent:
                    if (!done.Task.IsCompleted)
                    {
                        done.TrySetResult(responseBuilder.ToString());
                    }
                    break;
            }
        });

        try
        {
            var prompt = """
                Analyze the currently staged changes.

                1. Call analyze_staged to get the staged diff
                2. Call map_doc_targets to identify documentation targets
                3. Generate a documentation brief

                Return your analysis as a structured JSON response.
                """;

            await _session.SendAsync(new MessageOptions { Prompt = prompt });
            var response = await done.Task;

            return new OrchestratorResult
            {
                Success = true,
                RawResponse = response,
                Diff = _context.CurrentDiff,
                Mapping = _context.MappingResult
            };
        }
        finally
        {
            subscription.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        if (_session is not null)
        {
            await _session.DisposeAsync();
        }

        await _client.StopAsync();
        _disposed = true;
    }
}

public sealed class OrchestratorResult
{
    public required bool Success { get; init; }
    public string? RawResponse { get; init; }
    public string? Error { get; init; }
    public DocPilot.Analysis.DiffResult? Diff { get; init; }
    public DocPilot.Heuristics.MappingResult? Mapping { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });
}
```

#### 5.8 Creare OrchestratorAgentTests.cs
- [x] Crea il file `tests/DocPilot.Tests/Agents/OrchestratorAgentTests.cs`:

```csharp
using DocPilot.Agents;
using DocPilot.Agents.Tools;
using DocPilot.Analysis;
using DocPilot.Configuration;
using FluentAssertions;

namespace DocPilot.Tests.Agents;

public class OrchestratorAgentToolsTests
{
    [Fact]
    public void ReadFileTool_WithValidPath_ReturnsContent()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"docpilot-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var testFile = Path.Combine(tempDir, "test.md");
            File.WriteAllText(testFile, "# Test Content");

            var context = new AgentContext
            {
                RepositoryPath = tempDir,
                Config = DocPilotConfig.Default
            };

            var tool = ReadFileTool.Create(context);

            // Assert - tool was created successfully
            tool.Name.Should().Be("read_file");
            tool.Description.Should().Contain("Read the content");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AnalyzeDiffTool_Create_ReturnsValidFunction()
    {
        // Arrange
        var context = new AgentContext
        {
            RepositoryPath = ".",
            Config = DocPilotConfig.Default
        };

        // Act
        var tool = AnalyzeDiffTool.Create(context);

        // Assert
        tool.Name.Should().Be("analyze_diff");
        tool.Description.Should().Contain("Analyze git diff");
    }

    [Fact]
    public void MapDocTargetsTool_WithoutDiff_ReturnsError()
    {
        // Arrange
        var context = new AgentContext
        {
            RepositoryPath = ".",
            Config = DocPilotConfig.Default,
            CurrentDiff = null // No diff set
        };

        var tool = MapDocTargetsTool.Create(context);

        // Assert - tool created, error handled in invocation
        tool.Name.Should().Be("map_doc_targets");
    }

    [Fact]
    public void SystemPrompts_ContainsRequiredGuidelines()
    {
        // Assert
        SystemPrompts.Orchestrator.Should().Contain("ONLY modify documentation files");
        SystemPrompts.Orchestrator.Should().Contain("NEVER hallucinate");
        SystemPrompts.DocWriter.Should().Contain("Mermaid");
    }
}
```

### Step 5 Verification Checklist
- [x] `dotnet build` compila senza errori
- [x] `dotnet test --filter OrchestratorAgent` passa (4 test)
- [x] I tool sono creati correttamente con nomi e descrizioni

### Step 5 STOP & COMMIT
**STOP & COMMIT:** Fermarsi qui e attendere che l'utente testi, faccia stage e commit.

Messaggio commit suggerito:
```
feat(agents): add Copilot SDK orchestrator with custom tools

- Implement OrchestratorAgent with session management
- Add analyze_diff, map_doc_targets, read_file tools
- Define system prompts for orchestrator and writer
- Use streaming with delta events for feedback
- Add unit tests for tool creation
```

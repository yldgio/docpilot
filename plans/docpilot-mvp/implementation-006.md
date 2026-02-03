# DocPilot MVP - Implementation Guide (Part 6)

## Step 6: Doc Writer Agent

### Step-by-Step Instructions

#### 6.1 Creare directory Generation
- [x] Esegui:

```powershell
New-Item -ItemType Directory -Path "src/DocPilot/Generation" -Force
New-Item -ItemType Directory -Path "tests/DocPilot.Tests/Generation" -Force
```

#### 6.2 Creare DocPatch.cs
- [x] Crea il file `src/DocPilot/Generation/DocPatch.cs`:

```csharp
namespace DocPilot.Generation;

public enum PatchOperation
{
    Create,
    Update,
    Append,
    Delete
}

public sealed class DocPatch
{
    public required string FilePath { get; init; }
    public required PatchOperation Operation { get; init; }
    public required string Content { get; init; }
    public string? Section { get; init; }
    public List<string> MermaidBlocks { get; init; } = [];
    public List<string> SourceReferences { get; init; } = [];
    public double Confidence { get; init; }
    public string? Rationale { get; init; }
}

public sealed class PatchSet
{
    public required IReadOnlyList<DocPatch> Patches { get; init; }
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? Summary { get; init; }

    public int TotalPatches => Patches.Count;
    public int CreatedFiles => Patches.Count(p => p.Operation == PatchOperation.Create);
    public int UpdatedFiles => Patches.Count(p => p.Operation == PatchOperation.Update);
}
```

#### 6.3 Creare PatchApplier.cs
- [x] Crea il file `src/DocPilot/Generation/PatchApplier.cs`:

```csharp
namespace DocPilot.Generation;

public sealed class PatchApplier
{
    private readonly string _repositoryPath;

    public PatchApplier(string repositoryPath)
    {
        _repositoryPath = repositoryPath;
    }

    public async Task<ApplyResult> ApplyAsync(PatchSet patchSet, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        var results = new List<PatchResult>();

        foreach (var patch in patchSet.Patches)
        {
            var result = await ApplyPatchAsync(patch, dryRun, cancellationToken);
            results.Add(result);
        }

        return new ApplyResult
        {
            Success = results.All(r => r.Success),
            Results = results,
            DryRun = dryRun
        };
    }

    private async Task<PatchResult> ApplyPatchAsync(DocPatch patch, bool dryRun, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(_repositoryPath, patch.FilePath);

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                if (!dryRun)
                {
                    Directory.CreateDirectory(directory);
                }
            }

            var content = patch.Operation switch
            {
                PatchOperation.Create => patch.Content,
                PatchOperation.Update => patch.Content,
                PatchOperation.Append => await GetAppendedContentAsync(fullPath, patch, cancellationToken),
                PatchOperation.Delete => null,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (!dryRun)
            {
                if (patch.Operation == PatchOperation.Delete)
                {
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
                else if (content is not null)
                {
                    await File.WriteAllTextAsync(fullPath, content, cancellationToken);
                }
            }

            return new PatchResult
            {
                FilePath = patch.FilePath,
                Operation = patch.Operation,
                Success = true,
                PreviewContent = dryRun ? content : null
            };
        }
        catch (Exception ex)
        {
            return new PatchResult
            {
                FilePath = patch.FilePath,
                Operation = patch.Operation,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<string> GetAppendedContentAsync(string fullPath, DocPatch patch, CancellationToken cancellationToken)
    {
        var existingContent = "";

        if (File.Exists(fullPath))
        {
            existingContent = await File.ReadAllTextAsync(fullPath, cancellationToken);
        }

        if (patch.Section is not null && existingContent.Contains(patch.Section))
        {
            // Insert after section header
            var sectionIndex = existingContent.IndexOf(patch.Section, StringComparison.Ordinal);
            var endOfLine = existingContent.IndexOf('\n', sectionIndex);

            if (endOfLine == -1)
            {
                endOfLine = existingContent.Length;
            }

            return existingContent.Insert(endOfLine + 1, "\n" + patch.Content + "\n");
        }

        // Append at end
        return existingContent.TrimEnd() + "\n\n" + patch.Content;
    }
}

public sealed class ApplyResult
{
    public required bool Success { get; init; }
    public required IReadOnlyList<PatchResult> Results { get; init; }
    public required bool DryRun { get; init; }

    public IEnumerable<PatchResult> FailedPatches => Results.Where(r => !r.Success);
}

public sealed class PatchResult
{
    public required string FilePath { get; init; }
    public required PatchOperation Operation { get; init; }
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public string? PreviewContent { get; init; }
}
```

#### 6.4 Creare GenerateDocPatchTool.cs
- [x] Crea il file `src/DocPilot/Agents/Tools/GenerateDocPatchTool.cs`:

```csharp
using System.ComponentModel;
using DocPilot.Generation;
using Microsoft.Extensions.AI;

namespace DocPilot.Agents.Tools;

public static class GenerateDocPatchTool
{
    public static AIFunction Create(AgentContext context)
    {
        return AIFunctionFactory.Create(
            ([Description("Target file path for the documentation")] string filePath,
             [Description("Operation: create, update, or append")] string operation,
             [Description("The documentation content in markdown format")] string content,
             [Description("Optional section header to append to")] string? section = null) =>
            {
                var op = operation.ToLowerInvariant() switch
                {
                    "create" => PatchOperation.Create,
                    "update" => PatchOperation.Update,
                    "append" => PatchOperation.Append,
                    "delete" => PatchOperation.Delete,
                    _ => PatchOperation.Update
                };

                var patch = new DocPatch
                {
                    FilePath = filePath,
                    Operation = op,
                    Content = content,
                    Section = section,
                    SourceReferences = context.MappingResult?.Targets
                        .FirstOrDefault(t => t.FilePath == filePath)?.SourceFiles ?? [],
                    Confidence = context.MappingResult?.AverageConfidence ?? 0.5
                };

                context.GeneratedPatches.Add(filePath);

                return new
                {
                    success = true,
                    filePath = patch.FilePath,
                    operation = patch.Operation.ToString(),
                    contentLength = patch.Content.Length,
                    hasSection = patch.Section is not null
                };
            },
            "generate_doc_patch",
            "Generate a documentation patch for a specific file");
    }
}
```

#### 6.5 Creare ValidateMermaidTool.cs
- [x] Crea il file `src/DocPilot/Agents/Tools/ValidateMermaidTool.cs`:

```csharp
using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace DocPilot.Agents.Tools;

public static partial class ValidateMermaidTool
{
    public static AIFunction Create()
    {
        return AIFunctionFactory.Create(
            ([Description("Mermaid diagram content to validate")] string mermaidContent) =>
            {
                var issues = new List<string>();

                // Check for valid diagram type
                var validTypes = new[] { "flowchart", "sequenceDiagram", "classDiagram", "stateDiagram", "erDiagram", "gantt", "pie", "graph" };
                var hasValidType = validTypes.Any(t => mermaidContent.TrimStart().StartsWith(t, StringComparison.OrdinalIgnoreCase));

                if (!hasValidType)
                {
                    issues.Add("Diagram must start with a valid type (flowchart, sequenceDiagram, classDiagram, etc.)");
                }

                // Check for balanced brackets
                var openBrackets = mermaidContent.Count(c => c == '{' || c == '[' || c == '(');
                var closeBrackets = mermaidContent.Count(c => c == '}' || c == ']' || c == ')');

                if (openBrackets != closeBrackets)
                {
                    issues.Add("Unbalanced brackets detected");
                }

                // Check for arrow syntax
                if (mermaidContent.Contains("flowchart") || mermaidContent.Contains("graph"))
                {
                    if (!ArrowPattern().IsMatch(mermaidContent))
                    {
                        issues.Add("Flowchart should contain valid arrows (-->, --->, -.->)");
                    }
                }

                // Check for reasonable size
                var nodeCount = NodePattern().Matches(mermaidContent).Count;
                if (nodeCount > 20)
                {
                    issues.Add($"Diagram has {nodeCount} nodes. Consider splitting into multiple diagrams for readability.");
                }

                return new
                {
                    valid = issues.Count == 0,
                    issues,
                    nodeCount,
                    diagramType = validTypes.FirstOrDefault(t =>
                        mermaidContent.TrimStart().StartsWith(t, StringComparison.OrdinalIgnoreCase)) ?? "unknown"
                };
            },
            "validate_mermaid",
            "Validate Mermaid diagram syntax and structure");
    }

    [GeneratedRegex(@"-->|---->|-.->|==>")]
    private static partial Regex ArrowPattern();

    [GeneratedRegex(@"\[[^\]]+\]|\([^\)]+\)|\{[^\}]+\}")]
    private static partial Regex NodePattern();
}
```

#### 6.6 Creare DocWriterAgent.cs
- [x] Crea il file `src/DocPilot/Agents/DocWriterAgent.cs`:

```csharp
using System.Text.Json;
using DocPilot.Agents.Tools;
using DocPilot.Configuration;
using DocPilot.Generation;
using DocPilot.Heuristics;
using GitHub.Copilot.SDK;

namespace DocPilot.Agents;

public sealed class DocWriterAgent : IAsyncDisposable
{
    private readonly CopilotClient _client;
    private readonly AgentContext _context;
    private readonly List<DocPatch> _generatedPatches = [];
    private CopilotSession? _session;
    private bool _disposed;

    public DocWriterAgent(string repositoryPath, DocPilotConfig config)
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
                Content = SystemPrompts.DocWriter
            },
            Tools =
            [
                GenerateDocPatchTool.Create(_context),
                ValidateMermaidTool.Create(),
                ReadFileTool.Create(_context)
            ]
        });
    }

    public async Task<WriterResult> GenerateAsync(
        MappingResult mapping,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null)
        {
            throw new InvalidOperationException("Agent not initialized. Call InitializeAsync first.");
        }

        _context.MappingResult = mapping;

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
            var targetsJson = JsonSerializer.Serialize(mapping.Targets.Select(t => new
            {
                t.FilePath,
                t.Section,
                t.Confidence,
                t.Rationale,
                t.SourceFiles
            }));

            var prompt = $"""
                Generate documentation for the following targets:

                {targetsJson}

                For each target:
                1. Use read_file to check if the file exists and get current content
                2. Generate appropriate documentation based on the source files and rationale
                3. Include Mermaid diagrams where appropriate (architecture, flows)
                4. Use validate_mermaid to check diagram syntax
                5. Call generate_doc_patch for each file

                Focus on clarity and accuracy. Include code examples where relevant.
                """;

            await _session.SendAsync(new MessageOptions { Prompt = prompt });
            var response = await done.Task;

            return new WriterResult
            {
                Success = true,
                RawResponse = response,
                GeneratedFiles = _context.GeneratedPatches.ToList()
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

public sealed class WriterResult
{
    public required bool Success { get; init; }
    public string? RawResponse { get; init; }
    public string? Error { get; init; }
    public List<string> GeneratedFiles { get; init; } = [];
}
```

#### 6.7 Creare test per Generation
- [x] Crea il file `tests/DocPilot.Tests/Generation/PatchApplierTests.cs`:

```csharp
using DocPilot.Generation;
using FluentAssertions;

namespace DocPilot.Tests.Generation;

public class PatchApplierTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PatchApplier _applier;

    public PatchApplierTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"docpilot-patch-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _applier = new PatchApplier(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyAsync_CreateOperation_CreatesNewFile()
    {
        // Arrange
        var patchSet = new PatchSet
        {
            Patches =
            [
                new DocPatch
                {
                    FilePath = "docs/new-file.md",
                    Operation = PatchOperation.Create,
                    Content = "# New Documentation\n\nThis is new content."
                }
            ]
        };

        // Act
        var result = await _applier.ApplyAsync(patchSet);

        // Assert
        result.Success.Should().BeTrue();
        var filePath = Path.Combine(_tempDir, "docs", "new-file.md");
        File.Exists(filePath).Should().BeTrue();
        File.ReadAllText(filePath).Should().Contain("# New Documentation");
    }

    [Fact]
    public async Task ApplyAsync_AppendOperation_AppendsToExistingFile()
    {
        // Arrange
        var existingPath = Path.Combine(_tempDir, "README.md");
        await File.WriteAllTextAsync(existingPath, "# Existing Content\n\n## Section");

        var patchSet = new PatchSet
        {
            Patches =
            [
                new DocPatch
                {
                    FilePath = "README.md",
                    Operation = PatchOperation.Append,
                    Content = "New appended content",
                    Section = "## Section"
                }
            ]
        };

        // Act
        var result = await _applier.ApplyAsync(patchSet);

        // Assert
        result.Success.Should().BeTrue();
        var content = await File.ReadAllTextAsync(existingPath);
        content.Should().Contain("# Existing Content");
        content.Should().Contain("New appended content");
    }

    [Fact]
    public async Task ApplyAsync_DryRun_DoesNotModifyFiles()
    {
        // Arrange
        var patchSet = new PatchSet
        {
            Patches =
            [
                new DocPatch
                {
                    FilePath = "should-not-exist.md",
                    Operation = PatchOperation.Create,
                    Content = "Content"
                }
            ]
        };

        // Act
        var result = await _applier.ApplyAsync(patchSet, dryRun: true);

        // Assert
        result.Success.Should().BeTrue();
        result.DryRun.Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, "should-not-exist.md")).Should().BeFalse();
        result.Results[0].PreviewContent.Should().NotBeNull();
    }

    [Fact]
    public async Task ApplyAsync_UpdateOperation_ReplacesContent()
    {
        // Arrange
        var existingPath = Path.Combine(_tempDir, "update.md");
        await File.WriteAllTextAsync(existingPath, "Old content");

        var patchSet = new PatchSet
        {
            Patches =
            [
                new DocPatch
                {
                    FilePath = "update.md",
                    Operation = PatchOperation.Update,
                    Content = "New content"
                }
            ]
        };

        // Act
        var result = await _applier.ApplyAsync(patchSet);

        // Assert
        result.Success.Should().BeTrue();
        var content = await File.ReadAllTextAsync(existingPath);
        content.Should().Be("New content");
    }
}
```

### Step 6 Verification Checklist
- [x] `dotnet build` compila senza errori
- [x] `dotnet test --filter PatchApplier` passa (4 test)
- [x] Le patch Create/Update/Append funzionano correttamente

### Step 6 STOP & COMMIT
**STOP & COMMIT:** Fermarsi qui e attendere che l'utente testi, faccia stage e commit.

Messaggio commit suggerito:
```
feat(generation): add doc writer agent with patch system

- Implement DocPatch and PatchSet models
- Add PatchApplier with create/update/append/delete operations
- Create DocWriterAgent with Mermaid validation
- Support dry-run mode for previewing changes
- Add comprehensive unit tests
```

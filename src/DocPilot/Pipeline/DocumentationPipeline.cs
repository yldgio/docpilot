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

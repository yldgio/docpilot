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

        var tools = new[]
        {
            AnalyzeDiffTool.Create(_context),
            AnalyzeDiffTool.CreateForStaged(_context),
            MapDocTargetsTool.Create(_context),
            ReadFileTool.Create(_context)
        };

        Console.Error.WriteLine($"Registering {tools.Length} tools...");

        _session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = "gpt-4.1",
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = SystemPrompts.Orchestrator
            },
            Tools = tools
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
                    done.TrySetResult(msg.Data.Content ?? string.Empty);
                    break;

                case ToolExecutionStartEvent toolStart:
                    Console.WriteLine($"\n  → {toolStart.Data.ToolName}");
                    break;

                case SessionErrorEvent error:
                    Console.Error.WriteLine($"\n[ERROR] {error.Data.Message}");
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
                You MUST use your tools to complete this task. Do NOT simulate or describe what you would do.

                STEP 1: Call the analyze_diff tool with baseRef="{baseRef}" and headRef="{headRef}"
                STEP 2: After getting the diff result, call map_doc_targets
                STEP 3: Report your findings

                Execute the tools NOW.
                """;

            var result = await _session.SendAndWaitAsync(new MessageOptions { Prompt = prompt });
            var response = result?.Data.Content ?? responseBuilder.ToString();

            // Verify that tools were actually called successfully
            var hasMapping = _context.MappingResult is not null;

            return new OrchestratorResult
            {
                Success = hasMapping,
                RawResponse = response,
                Error = hasMapping ? null : response,
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
                """;

            var result = await _session.SendAndWaitAsync(new MessageOptions { Prompt = prompt });
            var response = result?.Data.Content ?? responseBuilder.ToString();

            // Verify that tools were actually called successfully
            var hasMapping = _context.MappingResult is not null;

            return new OrchestratorResult
            {
                Success = hasMapping,
                RawResponse = response,
                Error = hasMapping ? null : response,
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

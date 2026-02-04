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

            await _session.SendAndWaitAsync(
                new MessageOptions { Prompt = prompt },
                timeout: TimeSpan.FromMinutes(5));
            var response = responseBuilder.ToString();

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

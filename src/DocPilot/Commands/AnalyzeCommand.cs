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

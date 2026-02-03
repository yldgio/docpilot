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

using System.CommandLine;
using DocPilot.Configuration;
using DocPilot.GitHub;
using DocPilot.Pipeline;

namespace DocPilot.Commands;

public static class PrCommand
{
    public static Command Create()
    {
        var targetBranchOption = new Option<string?>(
            aliases: ["--target-branch", "-t"],
            description: "Target branch for the PR (default: main)");

        var draftOption = new Option<bool>(
            aliases: ["--draft", "-d"],
            description: "Create PR as draft");

        var titleOption = new Option<string?>(
            aliases: ["--title"],
            description: "PR title (auto-generated if not provided)");

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

        var command = new Command("pr", "Create a documentation pull request")
        {
            targetBranchOption,
            draftOption,
            titleOption,
            baseOption,
            headOption,
            stagedOption,
            configOption
        };

        command.SetHandler(async (context) =>
        {
            var targetBranch = context.ParseResult.GetValueForOption(targetBranchOption);
            var draft = context.ParseResult.GetValueForOption(draftOption);
            var title = context.ParseResult.GetValueForOption(titleOption);
            var baseRef = context.ParseResult.GetValueForOption(baseOption);
            var headRef = context.ParseResult.GetValueForOption(headOption);
            var staged = context.ParseResult.GetValueForOption(stagedOption);
            var configPath = context.ParseResult.GetValueForOption(configOption);
            var cancellationToken = context.GetCancellationToken();

            var loader = new ConfigurationLoader();
            var config = await loader.LoadAsync(configPath, cancellationToken);
            var repoPath = Directory.GetCurrentDirectory();

            // Run full pipeline
            await using var pipeline = new DocumentationPipeline(repoPath, config);
            await pipeline.InitializeAsync(cancellationToken);

            // 1. Analyze
            Console.WriteLine("📊 Analyzing changes...");
            var analysisResult = await pipeline.RunAnalyzeAsync(baseRef, headRef, staged, cancellationToken);

            if (!analysisResult.Success || analysisResult.Mapping is null)
            {
                Console.Error.WriteLine($"❌ Analysis failed: {analysisResult.Error}");
                context.ExitCode = 1;
                return;
            }

            // 2. Generate
            Console.WriteLine("✍️  Generating documentation...");
            var generateResult = await pipeline.RunGenerateAsync(analysisResult.Mapping, dryRun: false, cancellationToken);

            if (!generateResult.Success || generateResult.GeneratedFiles is null)
            {
                Console.Error.WriteLine($"❌ Generation failed: {generateResult.Error}");
                context.ExitCode = 1;
                return;
            }

            // 3. Create PR
            Console.WriteLine("🚀 Creating pull request...");
            var prCreator = new PullRequestCreator(repoPath, config);
            var prResult = await prCreator.CreateDocumentationPrAsync(
                analysisResult.Mapping,
                generateResult.GeneratedFiles,
                new PrOptions
                {
                    Title = title,
                    TargetBranch = targetBranch,
                    Draft = draft
                },
                cancellationToken);

            if (!prResult.Success)
            {
                Console.Error.WriteLine($"❌ PR creation failed: {prResult.Error}");
                context.ExitCode = 1;
                return;
            }

            Console.WriteLine($"\n✅ Documentation PR created successfully!");
            Console.WriteLine($"   Branch: {prResult.Branch?.Name}");
            Console.WriteLine($"   PR: #{prResult.PullRequest?.Number}");
            Console.WriteLine($"   URL: {prResult.PullRequest?.Url}");

            context.ExitCode = 0;
        });

        return command;
    }
}

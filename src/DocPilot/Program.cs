using System.CommandLine;

namespace DocPilot;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("DocPilot - Automated documentation PR generator")
        {
            CreateAnalyzeCommand(),
            CreateGenerateCommand(),
            CreatePrCommand()
        };

        return await rootCommand.InvokeAsync(args);
    }

    private static Command CreateAnalyzeCommand()
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

        var command = new Command("analyze", "Analyze code changes and identify documentation targets")
        {
            baseOption,
            headOption,
            stagedOption,
            outputOption
        };

        command.SetHandler(async (context) =>
        {
            var baseRef = context.ParseResult.GetValueForOption(baseOption);
            var headRef = context.ParseResult.GetValueForOption(headOption);
            var staged = context.ParseResult.GetValueForOption(stagedOption);
            var output = context.ParseResult.GetValueForOption(outputOption);

            Console.WriteLine($"Analyzing changes (base: {baseRef ?? "HEAD~1"}, head: {headRef ?? "HEAD"}, staged: {staged})...");
            Console.WriteLine("// TODO: Implement analysis pipeline");
            await Task.CompletedTask;
        });

        return command;
    }

    private static Command CreateGenerateCommand()
    {
        var dryRunOption = new Option<bool>(
            aliases: ["--dry-run", "-n"],
            description: "Preview changes without applying them");

        var targetOption = new Option<string?>(
            aliases: ["--target", "-t"],
            description: "Target directory for generated docs");

        var command = new Command("generate", "Generate documentation patches based on analysis")
        {
            dryRunOption,
            targetOption
        };

        command.SetHandler(async (context) =>
        {
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var target = context.ParseResult.GetValueForOption(targetOption);

            Console.WriteLine($"Generating documentation (dry-run: {dryRun}, target: {target ?? "."})...");
            Console.WriteLine("// TODO: Implement generation pipeline");
            await Task.CompletedTask;
        });

        return command;
    }

    private static Command CreatePrCommand()
    {
        var targetBranchOption = new Option<string?>(
            aliases: ["--target-branch", "-t"],
            description: "Target branch for the PR");

        var draftOption = new Option<bool>(
            aliases: ["--draft", "-d"],
            description: "Create PR as draft");

        var command = new Command("pr", "Create a documentation pull request")
        {
            targetBranchOption,
            draftOption
        };

        command.SetHandler(async (context) =>
        {
            var targetBranch = context.ParseResult.GetValueForOption(targetBranchOption);
            var draft = context.ParseResult.GetValueForOption(draftOption);

            Console.WriteLine($"Creating PR (target: {targetBranch ?? "main"}, draft: {draft})...");
            Console.WriteLine("// TODO: Implement PR creation");
            await Task.CompletedTask;
        });

        return command;
    }
}

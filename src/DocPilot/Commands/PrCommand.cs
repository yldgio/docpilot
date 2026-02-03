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

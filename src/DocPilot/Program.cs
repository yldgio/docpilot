using System.CommandLine;
using DocPilot.Commands;

namespace DocPilot;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("DocPilot - Automated documentation PR generator")
        {
            AnalyzeCommand.Create(),
            GenerateCommand.Create(),
            PrCommand.Create()
        };

        return await rootCommand.InvokeAsync(args);
    }
}

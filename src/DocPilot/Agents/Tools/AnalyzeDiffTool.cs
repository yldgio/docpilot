using System.ComponentModel;
using DocPilot.Analysis;
using Microsoft.Extensions.AI;

namespace DocPilot.Agents.Tools;

public static class AnalyzeDiffTool
{
    public static AIFunction Create(AgentContext context)
    {
        return AIFunctionFactory.Create(
            ([Description("Base git reference (commit SHA or branch)")] string baseRef,
             [Description("Head git reference (commit SHA or branch)")] string headRef) =>
            {
                using var analyzer = new DiffAnalyzer(context.RepositoryPath);
                var diff = analyzer.AnalyzeRange(baseRef, headRef);
                context.CurrentDiff = diff;

                return new
                {
                    baseRef = diff.BaseRef,
                    headRef = diff.HeadRef,
                    totalFiles = diff.TotalFilesChanged,
                    totalLinesAdded = diff.TotalLinesAdded,
                    totalLinesDeleted = diff.TotalLinesDeleted,
                    files = diff.Files.Select(f => new
                    {
                        path = f.Path,
                        kind = f.Kind.ToString(),
                        linesAdded = f.LinesAdded,
                        linesDeleted = f.LinesDeleted
                    })
                };
            },
            "analyze_diff",
            "Analyze git diff between two references to identify changed files");
    }

    public static AIFunction CreateForStaged(AgentContext context)
    {
        return AIFunctionFactory.Create(
            () =>
            {
                using var analyzer = new DiffAnalyzer(context.RepositoryPath);
                var diff = analyzer.AnalyzeStaged();
                context.CurrentDiff = diff;

                return new
                {
                    baseRef = diff.BaseRef,
                    headRef = diff.HeadRef,
                    totalFiles = diff.TotalFilesChanged,
                    files = diff.Files.Select(f => new
                    {
                        path = f.Path,
                        kind = f.Kind.ToString()
                    })
                };
            },
            "analyze_staged",
            "Analyze currently staged changes in the repository");
    }
}

using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace DocPilot.Agents.Tools;

public static partial class ValidateMermaidTool
{
    public static AIFunction Create()
    {
        return AIFunctionFactory.Create(
            ([Description("Mermaid diagram content to validate")] string mermaidContent) =>
            {
                var issues = new List<string>();

                // Check for valid diagram type
                var validTypes = new[] { "flowchart", "sequenceDiagram", "classDiagram", "stateDiagram", "erDiagram", "gantt", "pie", "graph" };
                var hasValidType = validTypes.Any(t => mermaidContent.TrimStart().StartsWith(t, StringComparison.OrdinalIgnoreCase));

                if (!hasValidType)
                {
                    issues.Add("Diagram must start with a valid type (flowchart, sequenceDiagram, classDiagram, etc.)");
                }

                // Check for balanced brackets
                var openBrackets = mermaidContent.Count(c => c == '{' || c == '[' || c == '(');
                var closeBrackets = mermaidContent.Count(c => c == '}' || c == ']' || c == ')');

                if (openBrackets != closeBrackets)
                {
                    issues.Add("Unbalanced brackets detected");
                }

                // Check for arrow syntax
                if (mermaidContent.Contains("flowchart") || mermaidContent.Contains("graph"))
                {
                    if (!ArrowPattern().IsMatch(mermaidContent))
                    {
                        issues.Add("Flowchart should contain valid arrows (-->, --->, -.->)");
                    }
                }

                // Check for reasonable size
                var nodeCount = NodePattern().Matches(mermaidContent).Count;
                if (nodeCount > 20)
                {
                    issues.Add($"Diagram has {nodeCount} nodes. Consider splitting into multiple diagrams for readability.");
                }

                return new
                {
                    valid = issues.Count == 0,
                    issues,
                    nodeCount,
                    diagramType = validTypes.FirstOrDefault(t =>
                        mermaidContent.TrimStart().StartsWith(t, StringComparison.OrdinalIgnoreCase)) ?? "unknown"
                };
            },
            "validate_mermaid",
            "Validate Mermaid diagram syntax and structure");
    }

    [GeneratedRegex(@"-->|---->|-.->|==>")]
    private static partial Regex ArrowPattern();

    [GeneratedRegex(@"\[[^\]]+\]|\([^\)]+\)|\{[^\}]+\}")]
    private static partial Regex NodePattern();
}

using System.ComponentModel;
using DocPilot.Heuristics;
using Microsoft.Extensions.AI;

namespace DocPilot.Agents.Tools;

public static class MapDocTargetsTool
{
    public static AIFunction Create(AgentContext context)
    {
        Func<object> handler = () =>
        {
            if (context.CurrentDiff is null)
            {
                return new 
                { 
                    error = "No diff available. Call analyze_diff first.", 
                    changeType = (string?)null,
                    overallConfidence = (string?)null,
                    averageConfidenceScore = 0.0,
                    targets = Array.Empty<object>()
                };
            }

            var mapper = new DocTargetMapper(context.Config);
            var result = mapper.MapToDocTargets(context.CurrentDiff);
            context.MappingResult = result;

            return new
            {
                error = (string?)null,
                changeType = result.OverallChangeType.ToString(),
                overallConfidence = result.OverallConfidence.ToString(),
                averageConfidenceScore = result.AverageConfidence,
                targets = result.Targets.Select(t => new
                {
                    filePath = t.FilePath,
                    section = t.Section,
                    confidence = t.Confidence.ToString(),
                    confidenceScore = t.ConfidenceScore,
                    rationale = t.Rationale,
                    sourceFiles = t.SourceFiles
                }).ToArray()
            };
        };

        return AIFunctionFactory.Create(
            handler,
            "map_doc_targets",
            "Map analyzed changes to documentation targets with confidence scores");
    }
}

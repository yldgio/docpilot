using DocPilot.Analysis;
using DocPilot.Configuration;

namespace DocPilot.Heuristics;

public sealed class DocTargetMapper
{
    private readonly DocPilotConfig _config;
    private readonly ChangeClassifier _classifier;

    public DocTargetMapper(DocPilotConfig config)
    {
        _config = config;
        _classifier = new ChangeClassifier();
    }

    public MappingResult MapToDocTargets(DiffResult diff)
    {
        var changeType = _classifier.ClassifyChange(diff);
        var targets = new Dictionary<string, DocTarget>();
        var totalConfidence = 0.0;
        var matchCount = 0;

        foreach (var file in diff.Files)
        {
            foreach (var rule in _config.Heuristics)
            {
                if (!MatchesGlob(rule.Pattern, file.Path))
                {
                    continue;
                }

                var docPath = ResolveDocTarget(rule.DocTarget, file.Path);
                var confidence = CalculateConfidence(file, rule, changeType);

                if (targets.TryGetValue(docPath, out var existing))
                {
                    // Merge with existing target
                    existing.SourceFiles.Add(file.Path);
                    // Update confidence if higher
                    if (confidence > existing.ConfidenceScore)
                    {
                        targets[docPath] = existing with
                        {
                            ConfidenceScore = confidence,
                            Confidence = ScoreToLevel(confidence)
                        };
                    }
                }
                else
                {
                    targets[docPath] = new DocTarget
                    {
                        FilePath = docPath,
                        Section = rule.Section,
                        ConfidenceScore = confidence,
                        Confidence = ScoreToLevel(confidence),
                        Rationale = GenerateRationale(file, rule, changeType),
                        SourceFiles = [file.Path]
                    };
                }

                totalConfidence += confidence;
                matchCount++;
            }
        }

        var avgConfidence = matchCount > 0 ? totalConfidence / matchCount : 0.0;

        return new MappingResult
        {
            OverallChangeType = changeType,
            Targets = targets.Values.ToList(),
            AverageConfidence = avgConfidence
        };
    }

    private static string ResolveDocTarget(string template, string sourcePath)
    {
        if (!template.Contains("{0}"))
        {
            return template;
        }

        // Extract package/module name from path like "packages/mypackage/src/..."
        var parts = sourcePath.Split('/', '\\');
        var packageIndex = Array.IndexOf(parts, "packages");

        if (packageIndex >= 0 && packageIndex + 1 < parts.Length)
        {
            return string.Format(template, parts[packageIndex + 1]);
        }

        return template.Replace("{0}", "main");
    }

    private double CalculateConfidence(ChangedFile file, HeuristicRule rule, ChangeType changeType)
    {
        var baseConfidence = 0.5;

        // Boost for rule-specific confidence
        baseConfidence += rule.ConfidenceBoost;

        // Boost for significant changes
        if (file.TotalLinesChanged > 50)
        {
            baseConfidence += 0.1;
        }

        // Boost for new files (need initial docs)
        if (file.Kind == ChangeKind.Added)
        {
            baseConfidence += 0.15;
        }

        // Boost for breaking changes (critical to document)
        if (changeType == ChangeType.Breaking)
        {
            baseConfidence += 0.2;
        }

        // Penalize for very small changes
        if (file.TotalLinesChanged < 5)
        {
            baseConfidence -= 0.1;
        }

        return Math.Clamp(baseConfidence, 0.0, 1.0);
    }

    private static ConfidenceLevel ScoreToLevel(double score) => score switch
    {
        < 0.5 => ConfidenceLevel.Low,
        <= 0.8 => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.High
    };

    private static string GenerateRationale(ChangedFile file, HeuristicRule rule, ChangeType changeType)
    {
        var action = file.Kind switch
        {
            ChangeKind.Added => "New file added",
            ChangeKind.Deleted => "File removed",
            ChangeKind.Renamed => "File renamed",
            _ => "File modified"
        };

        return $"{action}: {file.Path} matches pattern '{rule.Pattern}'. " +
               $"Change type: {changeType}. Lines changed: +{file.LinesAdded}/-{file.LinesDeleted}.";
    }

    private static bool MatchesGlob(string pattern, string path)
    {
        // Use DotNet.Glob for pattern matching
        var glob = DotNet.Globbing.Glob.Parse(pattern);
        return glob.IsMatch(path);
    }
}

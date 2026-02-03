using DocPilot.Analysis;

namespace DocPilot.Heuristics;

public sealed class ChangeClassifier
{
    private static readonly Dictionary<string, ChangeType> PathPatternToChangeType = new()
    {
        ["**/fix/**"] = ChangeType.Bugfix,
        ["**/bugfix/**"] = ChangeType.Bugfix,
        ["**/hotfix/**"] = ChangeType.Bugfix,
        ["**/feature/**"] = ChangeType.Feature,
        ["**/feat/**"] = ChangeType.Feature,
        ["**/refactor/**"] = ChangeType.Refactor,
        ["**/docs/**"] = ChangeType.Documentation,
        ["**/*.md"] = ChangeType.Documentation,
        ["**/terraform/**"] = ChangeType.Infrastructure,
        ["**/bicep/**"] = ChangeType.Infrastructure,
        ["**/infra/**"] = ChangeType.Infrastructure,
        ["**/.github/**"] = ChangeType.Configuration,
        ["**/config/**"] = ChangeType.Configuration,
        ["**/*.yml"] = ChangeType.Configuration,
        ["**/*.yaml"] = ChangeType.Configuration
    };

    public ChangeType ClassifyChange(DiffResult diff)
    {
        var typeCounts = new Dictionary<ChangeType, int>();

        foreach (var file in diff.Files)
        {
            var type = ClassifyFile(file.Path);
            typeCounts.TryGetValue(type, out var count);
            typeCounts[type] = count + 1;
        }

        if (typeCounts.Count == 0)
        {
            return ChangeType.Unknown;
        }

        // Check for breaking changes
        if (HasBreakingChangeIndicators(diff))
        {
            return ChangeType.Breaking;
        }

        // Return most common type
        return typeCounts.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    public ChangeType ClassifyFile(string filePath)
    {
        foreach (var (pattern, changeType) in PathPatternToChangeType)
        {
            if (MatchesGlob(pattern, filePath))
            {
                return changeType;
            }
        }

        // Default based on extension
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".cs" or ".ts" or ".js" or ".py" or ".go" => ChangeType.Feature,
            ".md" or ".txt" or ".rst" => ChangeType.Documentation,
            ".tf" or ".bicep" => ChangeType.Infrastructure,
            ".json" or ".yml" or ".yaml" or ".xml" => ChangeType.Configuration,
            _ => ChangeType.Unknown
        };
    }

    private static bool HasBreakingChangeIndicators(DiffResult diff)
    {
        foreach (var file in diff.Files)
        {
            // Check for deletions of public APIs
            if (file.Kind == ChangeKind.Deleted &&
                (file.Path.Contains("Controller") ||
                 file.Path.Contains("Service") ||
                 file.Path.Contains("Interface")))
            {
                return true;
            }

            // Check hunk content for breaking patterns
            foreach (var hunk in file.Hunks)
            {
                if (hunk.Content.Contains("[Obsolete") ||
                    hunk.Content.Contains("BREAKING") ||
                    hunk.Content.Contains("@deprecated"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MatchesGlob(string pattern, string path)
    {
        // Use DotNet.Glob for pattern matching
        var glob = DotNet.Globbing.Glob.Parse(pattern);
        return glob.IsMatch(path);
    }
}

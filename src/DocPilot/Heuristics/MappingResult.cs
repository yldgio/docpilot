namespace DocPilot.Heuristics;

public enum ConfidenceLevel
{
    Low,    // < 0.5 - creates draft PR
    Medium, // 0.5 - 0.8 - creates normal PR
    High    // > 0.8 - creates PR with ready-for-review label
}

public sealed record DocTarget
{
    public required string FilePath { get; init; }
    public string? Section { get; init; }
    public required ConfidenceLevel Confidence { get; init; }
    public required double ConfidenceScore { get; init; }
    public required string Rationale { get; init; }
    public List<string> SourceFiles { get; init; } = [];
}

public sealed class MappingResult
{
    public required ChangeType OverallChangeType { get; init; }
    public required IReadOnlyList<DocTarget> Targets { get; init; }
    public required double AverageConfidence { get; init; }

    public ConfidenceLevel OverallConfidence => AverageConfidence switch
    {
        < 0.5 => ConfidenceLevel.Low,
        <= 0.8 => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.High
    };
}

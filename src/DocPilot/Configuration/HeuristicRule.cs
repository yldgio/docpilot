namespace DocPilot.Configuration;

public sealed class HeuristicRule
{
    public required string Pattern { get; init; }
    public required string DocTarget { get; init; }
    public string? Section { get; init; }
    public double ConfidenceBoost { get; init; } = 0.0;
}

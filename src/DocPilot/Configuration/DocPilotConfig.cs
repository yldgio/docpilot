namespace DocPilot.Configuration;

public sealed class DocPilotConfig
{
    public PathsConfig Paths { get; init; } = new();
    public LimitsConfig Limits { get; init; } = new();
    public List<HeuristicRule> Heuristics { get; init; } = GetDefaultHeuristics();

    public static DocPilotConfig Default => new();

    private static List<HeuristicRule> GetDefaultHeuristics() =>
    [
        new HeuristicRule
        {
            Pattern = "src/**/*.cs",
            DocTarget = "README.md",
            Section = "## API Reference",
            ConfidenceBoost = 0.1
        },
        new HeuristicRule
        {
            Pattern = "src/**/Controllers/**",
            DocTarget = "docs/api.md",
            Section = "## Endpoints",
            ConfidenceBoost = 0.2
        },
        new HeuristicRule
        {
            Pattern = "packages/*/**",
            DocTarget = "packages/{0}/README.md",
            ConfidenceBoost = 0.15
        },
        new HeuristicRule
        {
            Pattern = "terraform/**",
            DocTarget = "docs/infrastructure.md",
            Section = "## Infrastructure",
            ConfidenceBoost = 0.1
        },
        new HeuristicRule
        {
            Pattern = "bicep/**",
            DocTarget = "docs/infrastructure.md",
            Section = "## Infrastructure",
            ConfidenceBoost = 0.1
        },
        new HeuristicRule
        {
            Pattern = ".github/workflows/**",
            DocTarget = "docs/ci-cd.md",
            Section = "## CI/CD",
            ConfidenceBoost = 0.1
        }
    ];
}

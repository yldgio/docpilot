namespace DocPilot.Configuration;

public sealed class LimitsConfig
{
    public int MaxFiles { get; init; } = 50;
    public int MaxLines { get; init; } = 5000;
    public int MaxTokensPerRequest { get; init; } = 8000;
}

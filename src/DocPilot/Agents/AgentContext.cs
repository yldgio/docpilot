using DocPilot.Analysis;
using DocPilot.Configuration;
using DocPilot.Heuristics;

namespace DocPilot.Agents;

public sealed class AgentContext
{
    public required string RepositoryPath { get; init; }
    public required DocPilotConfig Config { get; init; }
    public DiffResult? CurrentDiff { get; set; }
    public MappingResult? MappingResult { get; set; }
    public List<string> GeneratedPatches { get; } = [];
}

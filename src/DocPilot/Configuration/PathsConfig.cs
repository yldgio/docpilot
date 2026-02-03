namespace DocPilot.Configuration;

public sealed class PathsConfig
{
    public List<string> Allowlist { get; init; } = ["docs/**", "*.md", "README*"];
    public List<string> Ignorelist { get; init; } = [".git/**", "node_modules/**", "bin/**", "obj/**"];
}

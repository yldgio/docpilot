namespace DocPilot.Analysis;

public enum ChangeKind
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied
}

public sealed class ChangedFile
{
    public required string Path { get; init; }
    public required ChangeKind Kind { get; init; }
    public string? OldPath { get; init; }
    public int LinesAdded { get; init; }
    public int LinesDeleted { get; init; }
    public List<DiffHunk> Hunks { get; init; } = [];

    public int TotalLinesChanged => LinesAdded + LinesDeleted;
}

public sealed record DiffHunk
{
    public int OldStart { get; init; }
    public int OldCount { get; init; }
    public int NewStart { get; init; }
    public int NewCount { get; init; }
    public required string Content { get; init; }
}

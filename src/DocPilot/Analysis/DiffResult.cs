using DotNet.Globbing;

namespace DocPilot.Analysis;

public sealed class DiffResult
{
    public required string BaseRef { get; init; }
    public required string HeadRef { get; init; }
    public required IReadOnlyList<ChangedFile> Files { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public int TotalFilesChanged => Files.Count;
    public int TotalLinesAdded => Files.Sum(f => f.LinesAdded);
    public int TotalLinesDeleted => Files.Sum(f => f.LinesDeleted);

    public IEnumerable<ChangedFile> GetFilesByKind(ChangeKind kind) =>
        Files.Where(f => f.Kind == kind);

    public IEnumerable<ChangedFile> GetFilesByPattern(string globPattern)
    {
        var glob = Glob.Parse(globPattern);
        return Files.Where(f => glob.IsMatch(f.Path));
    }
}

namespace DocPilot.Generation;

public enum PatchOperation
{
    Create,
    Update,
    Append,
    Delete
}

public sealed class DocPatch
{
    public required string FilePath { get; init; }
    public required PatchOperation Operation { get; init; }
    public required string Content { get; init; }
    public string? Section { get; init; }
    public List<string> MermaidBlocks { get; init; } = [];
    public List<string> SourceReferences { get; init; } = [];
    public double Confidence { get; init; }
    public string? Rationale { get; init; }
}

public sealed class PatchSet
{
    public required IReadOnlyList<DocPatch> Patches { get; init; }
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? Summary { get; init; }

    public int TotalPatches => Patches.Count;
    public int CreatedFiles => Patches.Count(p => p.Operation == PatchOperation.Create);
    public int UpdatedFiles => Patches.Count(p => p.Operation == PatchOperation.Update);
}

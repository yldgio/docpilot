namespace DocPilot.Generation;

public sealed class PatchApplier
{
    private readonly string _repositoryPath;

    public PatchApplier(string repositoryPath)
    {
        _repositoryPath = repositoryPath;
    }

    public async Task<ApplyResult> ApplyAsync(PatchSet patchSet, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        var results = new List<PatchResult>();

        foreach (var patch in patchSet.Patches)
        {
            var result = await ApplyPatchAsync(patch, dryRun, cancellationToken);
            results.Add(result);
        }

        return new ApplyResult
        {
            Success = results.All(r => r.Success),
            Results = results,
            DryRun = dryRun
        };
    }

    private async Task<PatchResult> ApplyPatchAsync(DocPatch patch, bool dryRun, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(_repositoryPath, patch.FilePath);

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                if (!dryRun)
                {
                    Directory.CreateDirectory(directory);
                }
            }

            var content = patch.Operation switch
            {
                PatchOperation.Create => patch.Content,
                PatchOperation.Update => patch.Content,
                PatchOperation.Append => await GetAppendedContentAsync(fullPath, patch, cancellationToken),
                PatchOperation.Delete => null,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (!dryRun)
            {
                if (patch.Operation == PatchOperation.Delete)
                {
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
                else if (content is not null)
                {
                    await File.WriteAllTextAsync(fullPath, content, cancellationToken);
                }
            }

            return new PatchResult
            {
                FilePath = patch.FilePath,
                Operation = patch.Operation,
                Success = true,
                PreviewContent = dryRun ? content : null
            };
        }
        catch (Exception ex)
        {
            return new PatchResult
            {
                FilePath = patch.FilePath,
                Operation = patch.Operation,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<string> GetAppendedContentAsync(string fullPath, DocPatch patch, CancellationToken cancellationToken)
    {
        var existingContent = "";

        if (File.Exists(fullPath))
        {
            existingContent = await File.ReadAllTextAsync(fullPath, cancellationToken);
        }

        if (patch.Section is not null && existingContent.Contains(patch.Section))
        {
            // Insert after section header
            var sectionIndex = existingContent.IndexOf(patch.Section, StringComparison.Ordinal);
            var endOfLine = existingContent.IndexOf('\n', sectionIndex);

            if (endOfLine == -1)
            {
                // No newline found, append at end
                return existingContent + "\n" + patch.Content;
            }

            // Insert after the newline character
            return existingContent.Insert(endOfLine + 1, patch.Content + "\n");
        }

        // Append at end
        return existingContent.TrimEnd() + "\n\n" + patch.Content;
    }
}

public sealed class ApplyResult
{
    public required bool Success { get; init; }
    public required IReadOnlyList<PatchResult> Results { get; init; }
    public required bool DryRun { get; init; }

    public IEnumerable<PatchResult> FailedPatches => Results.Where(r => !r.Success);
}

public sealed class PatchResult
{
    public required string FilePath { get; init; }
    public required PatchOperation Operation { get; init; }
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public string? PreviewContent { get; init; }
}

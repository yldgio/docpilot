using LibGit2Sharp;

namespace DocPilot.Analysis;

public sealed class DiffAnalyzer : IDisposable
{
    private readonly Repository _repository;
    private bool _disposed;

    public DiffAnalyzer(string repositoryPath)
    {
        var repoRoot = Repository.Discover(repositoryPath)
            ?? throw new ArgumentException($"Not a git repository: {repositoryPath}");

        _repository = new Repository(repoRoot);
    }

    public DiffResult AnalyzeRange(string baseRef, string headRef)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var baseCommit = ResolveCommit(baseRef);
        var headCommit = ResolveCommit(headRef);

        var diff = _repository.Diff.Compare<Patch>(baseCommit.Tree, headCommit.Tree);

        var files = diff.Select(MapToChangedFile).ToList();

        return new DiffResult
        {
            BaseRef = baseRef,
            HeadRef = headRef,
            Files = files
        };
    }

    public DiffResult AnalyzeStaged()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var headCommit = _repository.Head.Tip;
        var diff = _repository.Diff.Compare<Patch>(
            headCommit?.Tree,
            DiffTargets.Index);

        var files = diff.Select(MapToChangedFile).ToList();

        return new DiffResult
        {
            BaseRef = "HEAD",
            HeadRef = "INDEX",
            Files = files
        };
    }

    public DiffResult AnalyzeWorktree()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var diff = _repository.Diff.Compare<Patch>(
            _repository.Head.Tip?.Tree,
            DiffTargets.WorkingDirectory);

        var files = diff.Select(MapToChangedFile).ToList();

        return new DiffResult
        {
            BaseRef = "HEAD",
            HeadRef = "WORKTREE",
            Files = files
        };
    }

    private Commit ResolveCommit(string reference)
    {
        var resolved = _repository.Lookup<Commit>(reference);

        if (resolved is not null)
        {
            return resolved;
        }

        var branch = _repository.Branches[reference];
        if (branch?.Tip is not null)
        {
            return branch.Tip;
        }

        throw new ArgumentException($"Cannot resolve reference: {reference}");
    }

    private static ChangedFile MapToChangedFile(PatchEntryChanges entry)
    {
        var hunks = new List<DiffHunk>();

        // Parse hunks from patch content
        if (!string.IsNullOrEmpty(entry.Patch))
        {
            hunks = ParseHunks(entry.Patch);
        }

        return new ChangedFile
        {
            Path = entry.Path,
            OldPath = entry.OldPath != entry.Path ? entry.OldPath : null,
            Kind = MapChangeKind(entry.Status),
            LinesAdded = entry.LinesAdded,
            LinesDeleted = entry.LinesDeleted,
            Hunks = hunks
        };
    }

    private static ChangeKind MapChangeKind(LibGit2Sharp.ChangeKind libgitKind) => libgitKind switch
    {
        LibGit2Sharp.ChangeKind.Added => Analysis.ChangeKind.Added,
        LibGit2Sharp.ChangeKind.Deleted => Analysis.ChangeKind.Deleted,
        LibGit2Sharp.ChangeKind.Modified => Analysis.ChangeKind.Modified,
        LibGit2Sharp.ChangeKind.Renamed => Analysis.ChangeKind.Renamed,
        LibGit2Sharp.ChangeKind.Copied => Analysis.ChangeKind.Copied,
        _ => Analysis.ChangeKind.Modified
    };

    private static List<DiffHunk> ParseHunks(string patch)
    {
        var hunks = new List<DiffHunk>();
        var lines = patch.Split('\n');
        var currentHunkContent = new System.Text.StringBuilder();
        DiffHunk? currentHunk = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("@@"))
            {
                if (currentHunk is not null)
                {
                    hunks.Add(currentHunk with { Content = currentHunkContent.ToString() });
                }

                var (oldStart, oldCount, newStart, newCount) = ParseHunkHeader(line);
                currentHunk = new DiffHunk
                {
                    OldStart = oldStart,
                    OldCount = oldCount,
                    NewStart = newStart,
                    NewCount = newCount,
                    Content = ""
                };
                currentHunkContent.Clear();
            }
            else if (currentHunk is not null)
            {
                currentHunkContent.AppendLine(line);
            }
        }

        if (currentHunk is not null)
        {
            hunks.Add(currentHunk with { Content = currentHunkContent.ToString() });
        }

        return hunks;
    }

    private static (int oldStart, int oldCount, int newStart, int newCount) ParseHunkHeader(string header)
    {
        // Format: @@ -oldStart,oldCount +newStart,newCount @@
        var match = System.Text.RegularExpressions.Regex.Match(
            header,
            @"@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@");

        if (!match.Success)
        {
            return (0, 0, 0, 0);
        }

        return (
            int.Parse(match.Groups[1].Value),
            match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1,
            int.Parse(match.Groups[3].Value),
            match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 1
        );
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _repository.Dispose();
            _disposed = true;
        }
    }
}

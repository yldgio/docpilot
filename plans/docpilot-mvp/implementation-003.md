# DocPilot MVP - Implementation Guide (Part 3)

## Step 3: Git Diff Analyzer

### Step-by-Step Instructions

#### 3.1 Creare directory Analysis
- [x] Esegui:

```powershell
New-Item -ItemType Directory -Path "src/DocPilot/Analysis" -Force
New-Item -ItemType Directory -Path "tests/DocPilot.Tests/Analysis" -Force
```

#### 3.2 Creare ChangedFile.cs
- [x] Crea il file `src/DocPilot/Analysis/ChangedFile.cs`:

```csharp
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

public sealed class DiffHunk
{
    public int OldStart { get; init; }
    public int OldCount { get; init; }
    public int NewStart { get; init; }
    public int NewCount { get; init; }
    public required string Content { get; init; }
}
```

#### 3.3 Creare DiffResult.cs
- [x] Crea il file `src/DocPilot/Analysis/DiffResult.cs`:

```csharp
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
        var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher();
        matcher.AddInclude(globPattern);

        return Files.Where(f =>
        {
            var result = matcher.Match(f.Path);
            return result.HasMatches;
        });
    }
}
```

#### 3.4 Aggiungere package FileSystemGlobbing
- [x] Esegui:

```powershell
dotnet add src/DocPilot/DocPilot.csproj package Microsoft.Extensions.FileSystemGlobbing
```

#### 3.5 Creare DiffAnalyzer.cs
- [x] Crea il file `src/DocPilot/Analysis/DiffAnalyzer.cs`:

```csharp
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

    private static ChangeKind MapChangeKind(ChangeKind libgitKind) => libgitKind switch
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
```

#### 3.6 Creare DiffAnalyzerTests.cs
- [x] Crea il file `tests/DocPilot.Tests/Analysis/DiffAnalyzerTests.cs`:

```csharp
using DocPilot.Analysis;
using FluentAssertions;
using LibGit2Sharp;

namespace DocPilot.Tests.Analysis;

public class DiffAnalyzerTests : IDisposable
{
    private readonly string _tempRepoPath;
    private readonly Repository _repo;

    public DiffAnalyzerTests()
    {
        _tempRepoPath = Path.Combine(Path.GetTempPath(), $"docpilot-repo-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRepoPath);

        Repository.Init(_tempRepoPath);
        _repo = new Repository(_tempRepoPath);

        // Create initial commit
        var signature = new Signature("Test", "test@test.com", DateTimeOffset.Now);
        File.WriteAllText(Path.Combine(_tempRepoPath, "README.md"), "# Test");
        Commands.Stage(_repo, "README.md");
        _repo.Commit("Initial commit", signature, signature);
    }

    public void Dispose()
    {
        _repo.Dispose();
        if (Directory.Exists(_tempRepoPath))
        {
            // Force delete .git folder
            foreach (var file in Directory.GetFiles(_tempRepoPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(_tempRepoPath, recursive: true);
        }
    }

    [Fact]
    public void AnalyzeRange_WithModifiedFile_ReturnsCorrectDiff()
    {
        // Arrange
        var baseCommit = _repo.Head.Tip.Sha;

        // Modify file and commit
        File.WriteAllText(Path.Combine(_tempRepoPath, "README.md"), "# Test\n\nModified content");
        var signature = new Signature("Test", "test@test.com", DateTimeOffset.Now);
        Commands.Stage(_repo, "README.md");
        _repo.Commit("Modify README", signature, signature);
        var headCommit = _repo.Head.Tip.Sha;

        using var analyzer = new DiffAnalyzer(_tempRepoPath);

        // Act
        var result = analyzer.AnalyzeRange(baseCommit, headCommit);

        // Assert
        result.Files.Should().ContainSingle();
        result.Files[0].Path.Should().Be("README.md");
        result.Files[0].Kind.Should().Be(ChangeKind.Modified);
        result.TotalLinesAdded.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AnalyzeRange_WithNewFile_ReturnsAddedKind()
    {
        // Arrange
        var baseCommit = _repo.Head.Tip.Sha;

        // Add new file and commit
        Directory.CreateDirectory(Path.Combine(_tempRepoPath, "src"));
        File.WriteAllText(Path.Combine(_tempRepoPath, "src", "Program.cs"), "class Program {}");
        var signature = new Signature("Test", "test@test.com", DateTimeOffset.Now);
        Commands.Stage(_repo, "src/Program.cs");
        _repo.Commit("Add Program.cs", signature, signature);
        var headCommit = _repo.Head.Tip.Sha;

        using var analyzer = new DiffAnalyzer(_tempRepoPath);

        // Act
        var result = analyzer.AnalyzeRange(baseCommit, headCommit);

        // Assert
        result.Files.Should().ContainSingle();
        result.Files[0].Kind.Should().Be(ChangeKind.Added);
    }

    [Fact]
    public void AnalyzeStaged_WithStagedChanges_ReturnsDiff()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempRepoPath, "README.md"), "# Modified");
        Commands.Stage(_repo, "README.md");

        using var analyzer = new DiffAnalyzer(_tempRepoPath);

        // Act
        var result = analyzer.AnalyzeStaged();

        // Assert
        result.Files.Should().ContainSingle();
        result.BaseRef.Should().Be("HEAD");
        result.HeadRef.Should().Be("INDEX");
    }

    [Fact]
    public void AnalyzeWorktree_WithUnstagedChanges_ReturnsDiff()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempRepoPath, "README.md"), "# Unstaged change");

        using var analyzer = new DiffAnalyzer(_tempRepoPath);

        // Act
        var result = analyzer.AnalyzeWorktree();

        // Assert
        result.Files.Should().ContainSingle();
        result.HeadRef.Should().Be("WORKTREE");
    }

    [Fact]
    public void Constructor_WithInvalidPath_ThrowsArgumentException()
    {
        // Act
        var act = () => new DiffAnalyzer("/nonexistent/path");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Not a git repository*");
    }

    [Fact]
    public void GetFilesByPattern_FiltersCorrectly()
    {
        // Arrange
        var baseCommit = _repo.Head.Tip.Sha;

        // Add multiple files
        Directory.CreateDirectory(Path.Combine(_tempRepoPath, "src"));
        Directory.CreateDirectory(Path.Combine(_tempRepoPath, "docs"));
        File.WriteAllText(Path.Combine(_tempRepoPath, "src", "App.cs"), "class App {}");
        File.WriteAllText(Path.Combine(_tempRepoPath, "docs", "guide.md"), "# Guide");

        var signature = new Signature("Test", "test@test.com", DateTimeOffset.Now);
        Commands.Stage(_repo, "*");
        _repo.Commit("Add files", signature, signature);
        var headCommit = _repo.Head.Tip.Sha;

        using var analyzer = new DiffAnalyzer(_tempRepoPath);
        var result = analyzer.AnalyzeRange(baseCommit, headCommit);

        // Act
        var csFiles = result.GetFilesByPattern("src/**/*.cs");
        var mdFiles = result.GetFilesByPattern("**/*.md");

        // Assert
        csFiles.Should().ContainSingle();
        mdFiles.Should().ContainSingle();
    }
}
```

### Step 3 Verification Checklist
- [x] `dotnet build` compila senza errori
- [x] `dotnet test --filter DiffAnalyzer` passa (6 test)
- [x] I test creano/eliminano repository temporanei correttamente

### Step 3 STOP & COMMIT
**STOP & COMMIT:** Fermarsi qui e attendere che l'utente testi, faccia stage e commit.

Messaggio commit suggerito:
```
feat(analysis): add git diff analyzer with LibGit2Sharp

- Implement DiffAnalyzer for range/staged/worktree analysis
- Add ChangedFile and DiffResult models
- Parse diff hunks from patch content
- Add glob pattern matching for file filtering
- Add comprehensive unit tests with temp repositories
```

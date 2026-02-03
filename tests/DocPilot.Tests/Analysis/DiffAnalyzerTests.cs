using DocPilot.Analysis;
using FluentAssertions;
using LibGit2Sharp;
using Xunit;

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
        result.Files[0].Kind.Should().Be(DocPilot.Analysis.ChangeKind.Modified);
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
        result.Files[0].Kind.Should().Be(DocPilot.Analysis.ChangeKind.Added);
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

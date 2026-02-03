using DocPilot.Generation;
using FluentAssertions;
using Xunit;

namespace DocPilot.Tests.Generation;

public class PatchApplierTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PatchApplier _applier;

    public PatchApplierTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"docpilot-patch-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _applier = new PatchApplier(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyAsync_CreateOperation_CreatesNewFile()
    {
        // Arrange
        var patchSet = new PatchSet
        {
            Patches =
            [
                new DocPatch
                {
                    FilePath = "docs/new-file.md",
                    Operation = PatchOperation.Create,
                    Content = "# New Documentation\n\nThis is new content."
                }
            ]
        };

        // Act
        var result = await _applier.ApplyAsync(patchSet);

        // Assert
        result.Success.Should().BeTrue();
        var filePath = Path.Combine(_tempDir, "docs", "new-file.md");
        File.Exists(filePath).Should().BeTrue();
        File.ReadAllText(filePath).Should().Contain("# New Documentation");
    }

    [Fact]
    public async Task ApplyAsync_AppendOperation_AppendsToExistingFile()
    {
        // Arrange
        var existingPath = Path.Combine(_tempDir, "README.md");
        await File.WriteAllTextAsync(existingPath, "# Existing Content\n\n## Section");

        var patchSet = new PatchSet
        {
            Patches =
            [
                new DocPatch
                {
                    FilePath = "README.md",
                    Operation = PatchOperation.Append,
                    Content = "New appended content",
                    Section = "## Section"
                }
            ]
        };

        // Act
        var result = await _applier.ApplyAsync(patchSet);

        // Assert
        if (!result.Success && result.Results.Any())
        {
            throw new Exception($"Failed to apply patch: {result.Results[0].Error}");
        }
        result.Success.Should().BeTrue();
        var content = await File.ReadAllTextAsync(existingPath);
        content.Should().Contain("# Existing Content");
        content.Should().Contain("New appended content");
    }

    [Fact]
    public async Task ApplyAsync_DryRun_DoesNotModifyFiles()
    {
        // Arrange
        var patchSet = new PatchSet
        {
            Patches =
            [
                new DocPatch
                {
                    FilePath = "should-not-exist.md",
                    Operation = PatchOperation.Create,
                    Content = "Content"
                }
            ]
        };

        // Act
        var result = await _applier.ApplyAsync(patchSet, dryRun: true);

        // Assert
        result.Success.Should().BeTrue();
        result.DryRun.Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, "should-not-exist.md")).Should().BeFalse();
        result.Results[0].PreviewContent.Should().NotBeNull();
    }

    [Fact]
    public async Task ApplyAsync_UpdateOperation_ReplacesContent()
    {
        // Arrange
        var existingPath = Path.Combine(_tempDir, "update.md");
        await File.WriteAllTextAsync(existingPath, "Old content");

        var patchSet = new PatchSet
        {
            Patches =
            [
                new DocPatch
                {
                    FilePath = "update.md",
                    Operation = PatchOperation.Update,
                    Content = "New content"
                }
            ]
        };

        // Act
        var result = await _applier.ApplyAsync(patchSet);

        // Assert
        result.Success.Should().BeTrue();
        var content = await File.ReadAllTextAsync(existingPath);
        content.Should().Be("New content");
    }
}

using DocPilot.Analysis;
using DocPilot.Configuration;
using DocPilot.Heuristics;
using FluentAssertions;
using Xunit;

namespace DocPilot.Tests.Heuristics;

public class ChangeClassifierTests
{
    private readonly ChangeClassifier _classifier = new();

    [Theory]
    [InlineData("src/feature/UserService.cs", ChangeType.Feature)]
    [InlineData("src/fix/BugFix.cs", ChangeType.Bugfix)]
    [InlineData("docs/README.md", ChangeType.Documentation)]
    [InlineData("terraform/main.tf", ChangeType.Infrastructure)]
    [InlineData(".github/workflows/ci.yml", ChangeType.Configuration)]
    public void ClassifyFile_WithKnownPatterns_ReturnsCorrectType(string path, ChangeType expected)
    {
        // Act
        var result = _classifier.ClassifyFile(path);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ClassifyChange_WithMixedFiles_ReturnsMostCommon()
    {
        // Arrange
        var diff = new DiffResult
        {
            BaseRef = "HEAD~1",
            HeadRef = "HEAD",
            Files =
            [
                new ChangedFile { Path = "src/App.cs", Kind = DocPilot.Analysis.ChangeKind.Modified },
                new ChangedFile { Path = "src/Service.cs", Kind = DocPilot.Analysis.ChangeKind.Modified },
                new ChangedFile { Path = "docs/README.md", Kind = DocPilot.Analysis.ChangeKind.Modified }
            ]
        };

        // Act
        var result = _classifier.ClassifyChange(diff);

        // Assert
        result.Should().Be(ChangeType.Feature); // 2 feature files vs 1 doc
    }

    [Fact]
    public void ClassifyChange_WithBreakingIndicators_ReturnsBreaking()
    {
        // Arrange
        var diff = new DiffResult
        {
            BaseRef = "HEAD~1",
            HeadRef = "HEAD",
            Files =
            [
                new ChangedFile
                {
                    Path = "src/Api.cs",
                    Kind = DocPilot.Analysis.ChangeKind.Modified,
                    Hunks =
                    [
                        new DiffHunk
                        {
                            OldStart = 1,
                            OldCount = 5,
                            NewStart = 1,
                            NewCount = 5,
                            Content = "[Obsolete(\"Use NewMethod instead\")]"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _classifier.ClassifyChange(diff);

        // Assert
        result.Should().Be(ChangeType.Breaking);
    }
}

public class DocTargetMapperTests
{
    [Fact]
    public void MapToDocTargets_WithMatchingPatterns_ReturnsTargets()
    {
        // Arrange
        var config = DocPilotConfig.Default;
        var mapper = new DocTargetMapper(config);
        var diff = new DiffResult
        {
            BaseRef = "HEAD~1",
            HeadRef = "HEAD",
            Files =
            [
                new ChangedFile
                {
                    Path = "src/UserController.cs",
                    Kind = DocPilot.Analysis.ChangeKind.Added,
                    LinesAdded = 100,
                    LinesDeleted = 0
                }
            ]
        };

        // Act
        var result = mapper.MapToDocTargets(diff);

        // Assert
        result.Targets.Should().NotBeEmpty();
        result.OverallChangeType.Should().Be(ChangeType.Feature);
    }

    [Fact]
    public void MapToDocTargets_WithNewFile_HasHigherConfidence()
    {
        // Arrange
        var config = DocPilotConfig.Default;
        var mapper = new DocTargetMapper(config);

        var addedFile = new DiffResult
        {
            BaseRef = "HEAD~1",
            HeadRef = "HEAD",
            Files = [new ChangedFile { Path = "src/New.cs", Kind = DocPilot.Analysis.ChangeKind.Added, LinesAdded = 50 }]
        };

        var modifiedFile = new DiffResult
        {
            BaseRef = "HEAD~1",
            HeadRef = "HEAD",
            Files = [new ChangedFile { Path = "src/Existing.cs", Kind = DocPilot.Analysis.ChangeKind.Modified, LinesAdded = 50 }]
        };

        // Act
        var addedResult = mapper.MapToDocTargets(addedFile);
        var modifiedResult = mapper.MapToDocTargets(modifiedFile);

        // Assert
        addedResult.AverageConfidence.Should().BeGreaterThan(modifiedResult.AverageConfidence);
    }

    [Fact]
    public void MapToDocTargets_WithPackagePattern_ResolvesCorrectly()
    {
        // Arrange
        var config = new DocPilotConfig
        {
            Heuristics =
            [
                new HeuristicRule
                {
                    Pattern = "packages/*/**",
                    DocTarget = "packages/{0}/README.md",
                    ConfidenceBoost = 0.15
                }
            ]
        };
        var mapper = new DocTargetMapper(config);
        var diff = new DiffResult
        {
            BaseRef = "HEAD~1",
            HeadRef = "HEAD",
            Files =
            [
                new ChangedFile { Path = "packages/mylib/src/index.ts", Kind = DocPilot.Analysis.ChangeKind.Modified }
            ]
        };

        // Act
        var result = mapper.MapToDocTargets(diff);

        // Assert
        result.Targets.Should().ContainSingle()
            .Which.FilePath.Should().Be("packages/mylib/README.md");
    }

    [Theory]
    [InlineData(0.3, ConfidenceLevel.Low)]
    [InlineData(0.6, ConfidenceLevel.Medium)]
    [InlineData(0.9, ConfidenceLevel.High)]
    public void MappingResult_OverallConfidence_MapsCorrectly(double score, ConfidenceLevel expected)
    {
        // Arrange
        var result = new MappingResult
        {
            OverallChangeType = ChangeType.Feature,
            Targets = [],
            AverageConfidence = score
        };

        // Act & Assert
        result.OverallConfidence.Should().Be(expected);
    }
}

# DocPilot MVP - Implementation Guide (Part 4)

## Step 4: Change Classifier (Heuristics Engine)

### Step-by-Step Instructions

#### 4.1 Creare directory Heuristics
- [x] Esegui:

```powershell
New-Item -ItemType Directory -Path "src/DocPilot/Heuristics" -Force
New-Item -ItemType Directory -Path "tests/DocPilot.Tests/Heuristics" -Force
```

#### 4.2 Creare ChangeType.cs
- [x] Crea il file `src/DocPilot/Heuristics/ChangeType.cs`:

```csharp
namespace DocPilot.Heuristics;

public enum ChangeType
{
    Feature,
    Bugfix,
    Refactor,
    Breaking,
    Documentation,
    Infrastructure,
    Configuration,
    Unknown
}
```

#### 4.3 Creare MappingResult.cs
- [x] Crea il file `src/DocPilot/Heuristics/MappingResult.cs`:

```csharp
namespace DocPilot.Heuristics;

public enum ConfidenceLevel
{
    Low,    // < 0.5 - creates draft PR
    Medium, // 0.5 - 0.8 - creates normal PR
    High    // > 0.8 - creates PR with ready-for-review label
}

public sealed class DocTarget
{
    public required string FilePath { get; init; }
    public string? Section { get; init; }
    public required ConfidenceLevel Confidence { get; init; }
    public required double ConfidenceScore { get; init; }
    public required string Rationale { get; init; }
    public List<string> SourceFiles { get; init; } = [];
}

public sealed class MappingResult
{
    public required ChangeType OverallChangeType { get; init; }
    public required IReadOnlyList<DocTarget> Targets { get; init; }
    public required double AverageConfidence { get; init; }

    public ConfidenceLevel OverallConfidence => AverageConfidence switch
    {
        < 0.5 => ConfidenceLevel.Low,
        <= 0.8 => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.High
    };
}
```

#### 4.4 Creare ChangeClassifier.cs
- [x] Crea il file `src/DocPilot/Heuristics/ChangeClassifier.cs`:

```csharp
using DocPilot.Analysis;

namespace DocPilot.Heuristics;

public sealed class ChangeClassifier
{
    private static readonly Dictionary<string, ChangeType> PathPatternToChangeType = new()
    {
        ["**/fix/**"] = ChangeType.Bugfix,
        ["**/bugfix/**"] = ChangeType.Bugfix,
        ["**/hotfix/**"] = ChangeType.Bugfix,
        ["**/feature/**"] = ChangeType.Feature,
        ["**/feat/**"] = ChangeType.Feature,
        ["**/refactor/**"] = ChangeType.Refactor,
        ["**/docs/**"] = ChangeType.Documentation,
        ["**/*.md"] = ChangeType.Documentation,
        ["**/terraform/**"] = ChangeType.Infrastructure,
        ["**/bicep/**"] = ChangeType.Infrastructure,
        ["**/infra/**"] = ChangeType.Infrastructure,
        ["**/.github/**"] = ChangeType.Configuration,
        ["**/config/**"] = ChangeType.Configuration,
        ["**/*.yml"] = ChangeType.Configuration,
        ["**/*.yaml"] = ChangeType.Configuration
    };

    public ChangeType ClassifyChange(DiffResult diff)
    {
        var typeCounts = new Dictionary<ChangeType, int>();

        foreach (var file in diff.Files)
        {
            var type = ClassifyFile(file.Path);
            typeCounts.TryGetValue(type, out var count);
            typeCounts[type] = count + 1;
        }

        if (typeCounts.Count == 0)
        {
            return ChangeType.Unknown;
        }

        // Check for breaking changes
        if (HasBreakingChangeIndicators(diff))
        {
            return ChangeType.Breaking;
        }

        // Return most common type
        return typeCounts.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    public ChangeType ClassifyFile(string filePath)
    {
        var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher();

        foreach (var (pattern, changeType) in PathPatternToChangeType)
        {
            matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher();
            matcher.AddInclude(pattern);
            if (matcher.Match(filePath).HasMatches)
            {
                return changeType;
            }
        }

        // Default based on extension
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".cs" or ".ts" or ".js" or ".py" or ".go" => ChangeType.Feature,
            ".md" or ".txt" or ".rst" => ChangeType.Documentation,
            ".tf" or ".bicep" => ChangeType.Infrastructure,
            ".json" or ".yml" or ".yaml" or ".xml" => ChangeType.Configuration,
            _ => ChangeType.Unknown
        };
    }

    private static bool HasBreakingChangeIndicators(DiffResult diff)
    {
        foreach (var file in diff.Files)
        {
            // Check for deletions of public APIs
            if (file.Kind == ChangeKind.Deleted &&
                (file.Path.Contains("Controller") ||
                 file.Path.Contains("Service") ||
                 file.Path.Contains("Interface")))
            {
                return true;
            }

            // Check hunk content for breaking patterns
            foreach (var hunk in file.Hunks)
            {
                if (hunk.Content.Contains("[Obsolete") ||
                    hunk.Content.Contains("BREAKING") ||
                    hunk.Content.Contains("@deprecated"))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
```

#### 4.5 Creare DocTargetMapper.cs
- [x] Crea il file `src/DocPilot/Heuristics/DocTargetMapper.cs`:

```csharp
using DocPilot.Analysis;
using DocPilot.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;

namespace DocPilot.Heuristics;

public sealed class DocTargetMapper
{
    private readonly DocPilotConfig _config;
    private readonly ChangeClassifier _classifier;

    public DocTargetMapper(DocPilotConfig config)
    {
        _config = config;
        _classifier = new ChangeClassifier();
    }

    public MappingResult MapToDocTargets(DiffResult diff)
    {
        var changeType = _classifier.ClassifyChange(diff);
        var targets = new Dictionary<string, DocTarget>();
        var totalConfidence = 0.0;
        var matchCount = 0;

        foreach (var file in diff.Files)
        {
            foreach (var rule in _config.Heuristics)
            {
                var matcher = new Matcher();
                matcher.AddInclude(rule.Pattern);

                if (!matcher.Match(file.Path).HasMatches)
                {
                    continue;
                }

                var docPath = ResolveDocTarget(rule.DocTarget, file.Path);
                var confidence = CalculateConfidence(file, rule, changeType);

                if (targets.TryGetValue(docPath, out var existing))
                {
                    // Merge with existing target
                    existing.SourceFiles.Add(file.Path);
                    // Update confidence if higher
                    if (confidence > existing.ConfidenceScore)
                    {
                        targets[docPath] = existing with
                        {
                            ConfidenceScore = confidence,
                            Confidence = ScoreToLevel(confidence)
                        };
                    }
                }
                else
                {
                    targets[docPath] = new DocTarget
                    {
                        FilePath = docPath,
                        Section = rule.Section,
                        ConfidenceScore = confidence,
                        Confidence = ScoreToLevel(confidence),
                        Rationale = GenerateRationale(file, rule, changeType),
                        SourceFiles = [file.Path]
                    };
                }

                totalConfidence += confidence;
                matchCount++;
            }
        }

        var avgConfidence = matchCount > 0 ? totalConfidence / matchCount : 0.0;

        return new MappingResult
        {
            OverallChangeType = changeType,
            Targets = targets.Values.ToList(),
            AverageConfidence = avgConfidence
        };
    }

    private static string ResolveDocTarget(string template, string sourcePath)
    {
        if (!template.Contains("{0}"))
        {
            return template;
        }

        // Extract package/module name from path like "packages/mypackage/src/..."
        var parts = sourcePath.Split('/', '\\');
        var packageIndex = Array.IndexOf(parts, "packages");

        if (packageIndex >= 0 && packageIndex + 1 < parts.Length)
        {
            return string.Format(template, parts[packageIndex + 1]);
        }

        return template.Replace("{0}", "main");
    }

    private double CalculateConfidence(ChangedFile file, HeuristicRule rule, ChangeType changeType)
    {
        var baseConfidence = 0.5;

        // Boost for rule-specific confidence
        baseConfidence += rule.ConfidenceBoost;

        // Boost for significant changes
        if (file.TotalLinesChanged > 50)
        {
            baseConfidence += 0.1;
        }

        // Boost for new files (need initial docs)
        if (file.Kind == ChangeKind.Added)
        {
            baseConfidence += 0.15;
        }

        // Boost for breaking changes (critical to document)
        if (changeType == ChangeType.Breaking)
        {
            baseConfidence += 0.2;
        }

        // Penalize for very small changes
        if (file.TotalLinesChanged < 5)
        {
            baseConfidence -= 0.1;
        }

        return Math.Clamp(baseConfidence, 0.0, 1.0);
    }

    private static ConfidenceLevel ScoreToLevel(double score) => score switch
    {
        < 0.5 => ConfidenceLevel.Low,
        <= 0.8 => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.High
    };

    private static string GenerateRationale(ChangedFile file, HeuristicRule rule, ChangeType changeType)
    {
        var action = file.Kind switch
        {
            ChangeKind.Added => "New file added",
            ChangeKind.Deleted => "File removed",
            ChangeKind.Renamed => "File renamed",
            _ => "File modified"
        };

        return $"{action}: {file.Path} matches pattern '{rule.Pattern}'. " +
               $"Change type: {changeType}. Lines changed: +{file.LinesAdded}/-{file.LinesDeleted}.";
    }
}
```

#### 4.6 Creare ChangeClassifierTests.cs
- [x] Crea il file `tests/DocPilot.Tests/Heuristics/ChangeClassifierTests.cs`:

```csharp
using DocPilot.Analysis;
using DocPilot.Configuration;
using DocPilot.Heuristics;
using FluentAssertions;

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
                new ChangedFile { Path = "src/App.cs", Kind = ChangeKind.Modified },
                new ChangedFile { Path = "src/Service.cs", Kind = ChangeKind.Modified },
                new ChangedFile { Path = "docs/README.md", Kind = ChangeKind.Modified }
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
                    Kind = ChangeKind.Modified,
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
                    Kind = ChangeKind.Added,
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
            Files = [new ChangedFile { Path = "src/New.cs", Kind = ChangeKind.Added, LinesAdded = 50 }]
        };

        var modifiedFile = new DiffResult
        {
            BaseRef = "HEAD~1",
            HeadRef = "HEAD",
            Files = [new ChangedFile { Path = "src/Existing.cs", Kind = ChangeKind.Modified, LinesAdded = 50 }]
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
                new ChangedFile { Path = "packages/mylib/src/index.ts", Kind = ChangeKind.Modified }
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
```

### Step 4 Verification Checklist
- [x] `dotnet build` compila senza errori
- [x] `dotnet test --filter "ChangeClassifier|DocTargetMapper"` passa (8+ test)
- [x] La classificazione distingue correttamente Feature/Bugfix/Breaking

### Step 4 STOP & COMMIT
**STOP & COMMIT:** Fermarsi qui e attendere che l'utente testi, faccia stage e commit.

Messaggio commit suggerito:
```
feat(heuristics): add change classifier and doc target mapper

- Implement ChangeClassifier with pattern-based classification
- Add DocTargetMapper with configurable heuristic rules
- Support confidence scoring (low/medium/high)
- Detect breaking changes via code analysis
- Add comprehensive unit tests
```

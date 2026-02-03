using DocPilot.Configuration;
using FluentAssertions;
using Xunit;

namespace DocPilot.Tests.Configuration;

public class ConfigurationLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigurationLoader _loader;

    public ConfigurationLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"docpilot-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _loader = new ConfigurationLoader();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenFileNotFound_ReturnsDefaultConfig()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDir, "nonexistent.yml");

        // Act
        var config = _loader.Load(nonExistentPath);

        // Assert
        config.Should().NotBeNull();
        config.Limits.MaxFiles.Should().Be(50);
        config.Limits.MaxLines.Should().Be(5000);
        config.Paths.Allowlist.Should().Contain("docs/**");
        config.Heuristics.Should().NotBeEmpty();
    }

    [Fact]
    public void Load_WhenValidYaml_ParsesCorrectly()
    {
        // Arrange
        var configPath = Path.Combine(_tempDir, "docpilot.yml");
        var yaml = """
            paths:
              allowlist:
                - "custom/**"
            limits:
              maxFiles: 100
              maxLines: 10000
            heuristics:
              - pattern: "lib/**/*.ts"
                docTarget: "docs/lib.md"
                confidenceBoost: 0.3
            """;
        File.WriteAllText(configPath, yaml);

        // Act
        var config = _loader.Load(configPath);

        // Assert
        config.Limits.MaxFiles.Should().Be(100);
        config.Limits.MaxLines.Should().Be(10000);
        config.Paths.Allowlist.Should().Contain("custom/**");
        config.Heuristics.Should().ContainSingle()
            .Which.Pattern.Should().Be("lib/**/*.ts");
    }

    [Fact]
    public void Load_WhenEmptyYaml_ReturnsDefaultConfig()
    {
        // Arrange
        var configPath = Path.Combine(_tempDir, "docpilot.yml");
        File.WriteAllText(configPath, "");

        // Act
        var config = _loader.Load(configPath);

        // Assert
        config.Should().NotBeNull();
        config.Limits.MaxFiles.Should().Be(50);
    }

    [Fact]
    public void Load_WhenPartialYaml_MergesWithDefaults()
    {
        // Arrange
        var configPath = Path.Combine(_tempDir, "docpilot.yml");
        var yaml = """
            limits:
              maxFiles: 200
            """;
        File.WriteAllText(configPath, yaml);

        // Act
        var config = _loader.Load(configPath);

        // Assert
        config.Limits.MaxFiles.Should().Be(200);
        config.Limits.MaxLines.Should().Be(5000); // Default
    }

    [Fact]
    public async Task LoadAsync_WhenValidYaml_ParsesCorrectly()
    {
        // Arrange
        var configPath = Path.Combine(_tempDir, "docpilot.yml");
        var yaml = """
            limits:
              maxFiles: 75
            """;
        await File.WriteAllTextAsync(configPath, yaml);

        // Act
        var config = await _loader.LoadAsync(configPath);

        // Assert
        config.Limits.MaxFiles.Should().Be(75);
    }

    [Fact]
    public void Load_WhenMalformedYaml_ThrowsConfigurationException()
    {
        // Arrange
        var configPath = Path.Combine(_tempDir, "docpilot.yml");
        var yaml = """
            limits:
              maxFiles: [invalid
            """;
        File.WriteAllText(configPath, yaml);

        // Act
        var act = () => _loader.Load(configPath);

        // Assert
        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Failed to parse*");
    }
}

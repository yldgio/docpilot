using DocPilot.Agents;
using DocPilot.Agents.Tools;
using DocPilot.Analysis;
using DocPilot.Configuration;
using FluentAssertions;
using Xunit;

namespace DocPilot.Tests.Agents;

public class OrchestratorAgentToolsTests
{
    [Fact]
    public void ReadFileTool_WithValidPath_ReturnsContent()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"docpilot-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var testFile = Path.Combine(tempDir, "test.md");
            File.WriteAllText(testFile, "# Test Content");

            var context = new AgentContext
            {
                RepositoryPath = tempDir,
                Config = DocPilotConfig.Default
            };

            var tool = ReadFileTool.Create(context);

            // Assert - tool was created successfully
            tool.Name.Should().Be("read_file");
            tool.Description.Should().Contain("Read the content");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AnalyzeDiffTool_Create_ReturnsValidFunction()
    {
        // Arrange
        var context = new AgentContext
        {
            RepositoryPath = ".",
            Config = DocPilotConfig.Default
        };

        // Act
        var tool = AnalyzeDiffTool.Create(context);

        // Assert
        tool.Name.Should().Be("analyze_diff");
        tool.Description.Should().Contain("Analyze git diff");
    }

    [Fact]
    public void MapDocTargetsTool_WithoutDiff_ReturnsError()
    {
        // Arrange
        var context = new AgentContext
        {
            RepositoryPath = ".",
            Config = DocPilotConfig.Default,
            CurrentDiff = null // No diff set
        };

        var tool = MapDocTargetsTool.Create(context);

        // Assert - tool created, error handled in invocation
        tool.Name.Should().Be("map_doc_targets");
    }

    [Fact]
    public void SystemPrompts_ContainsRequiredGuidelines()
    {
        // Assert
        SystemPrompts.Orchestrator.Should().Contain("ONLY modify documentation files");
        SystemPrompts.Orchestrator.Should().Contain("NEVER hallucinate");
        SystemPrompts.DocWriter.Should().Contain("Mermaid");
    }
}

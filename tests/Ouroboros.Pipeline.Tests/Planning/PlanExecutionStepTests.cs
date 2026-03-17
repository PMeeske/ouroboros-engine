using FluentAssertions;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class PlanExecutionStepTests
{
    [Fact]
    public void Constructor_RequiredToolName_CanBeSet()
    {
        // Arrange & Act
        var step = new PlanExecutionStep { ToolName = "my_tool" };

        // Assert
        step.ToolName.Should().Be("my_tool");
    }

    [Fact]
    public void Input_DefaultsToNull()
    {
        // Arrange & Act
        var step = new PlanExecutionStep { ToolName = "tool" };

        // Assert
        step.Input.Should().BeNull();
    }

    [Fact]
    public void Output_DefaultsToNull()
    {
        // Arrange & Act
        var step = new PlanExecutionStep { ToolName = "tool" };

        // Assert
        step.Output.Should().BeNull();
    }

    [Fact]
    public void Success_DefaultsToFalse()
    {
        // Arrange & Act
        var step = new PlanExecutionStep { ToolName = "tool" };

        // Assert
        step.Success.Should().BeFalse();
    }

    [Fact]
    public void Error_DefaultsToNull()
    {
        // Arrange & Act
        var step = new PlanExecutionStep { ToolName = "tool" };

        // Assert
        step.Error.Should().BeNull();
    }

    [Fact]
    public void Duration_CalculatesCorrectly()
    {
        // Arrange
        var start = new DateTime(2025, 1, 1, 12, 0, 0);
        var end = new DateTime(2025, 1, 1, 12, 0, 5);
        var step = new PlanExecutionStep
        {
            ToolName = "tool",
            StartTime = start,
            EndTime = end
        };

        // Act
        var duration = step.Duration;

        // Assert
        duration.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Duration_WhenStartAndEndSame_ReturnsZero()
    {
        // Arrange
        var time = DateTime.UtcNow;
        var step = new PlanExecutionStep
        {
            ToolName = "tool",
            StartTime = time,
            EndTime = time
        };

        // Act
        var duration = step.Duration;

        // Assert
        duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void AllProperties_CanBeSetAndRead()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddMinutes(1);

        // Act
        var step = new PlanExecutionStep
        {
            ToolName = "summarize_tool",
            Input = "some input text",
            Output = "summarized output",
            Success = true,
            Error = null,
            StartTime = start,
            EndTime = end
        };

        // Assert
        step.ToolName.Should().Be("summarize_tool");
        step.Input.Should().Be("some input text");
        step.Output.Should().Be("summarized output");
        step.Success.Should().BeTrue();
        step.Error.Should().BeNull();
        step.StartTime.Should().Be(start);
        step.EndTime.Should().Be(end);
        step.Duration.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Error_CanBeSetWhenFailed()
    {
        // Arrange & Act
        var step = new PlanExecutionStep
        {
            ToolName = "failing_tool",
            Success = false,
            Error = "Connection timeout"
        };

        // Assert
        step.Success.Should().BeFalse();
        step.Error.Should().Be("Connection timeout");
    }
}

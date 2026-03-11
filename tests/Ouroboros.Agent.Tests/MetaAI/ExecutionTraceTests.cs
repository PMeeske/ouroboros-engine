using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class ExecutionTraceTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var steps = new List<ExecutedStep>
        {
            new ExecutedStep("Step1", true, TimeSpan.FromSeconds(1), new Dictionary<string, object>())
        };
        int failedAtIndex = 0;
        string failureReason = "Step failed due to timeout";

        // Act
        var sut = new ExecutionTrace(steps, failedAtIndex, failureReason);

        // Assert
        sut.Steps.Should().HaveCount(1);
        sut.FailedAtIndex.Should().Be(failedAtIndex);
        sut.FailureReason.Should().Be(failureReason);
    }

    [Fact]
    public void Constructor_EmptyStepsList_IsAllowed()
    {
        // Arrange
        var steps = new List<ExecutedStep>();

        // Act
        var sut = new ExecutionTrace(steps, -1, "No steps executed");

        // Assert
        sut.Steps.Should().BeEmpty();
        sut.FailedAtIndex.Should().Be(-1);
        sut.FailureReason.Should().Be("No steps executed");
    }

    [Fact]
    public void Constructor_NegativeIndex_IsAllowed()
    {
        // Arrange & Act
        var sut = new ExecutionTrace(new List<ExecutedStep>(), -1, "Not applicable");

        // Assert
        sut.FailedAtIndex.Should().Be(-1);
    }
}

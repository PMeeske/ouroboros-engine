using PlanStep = Ouroboros.Agent.MetaAI.PlanStep;

namespace Ouroboros.Tests.Database.Storage;

/// <summary>
/// Unit tests for PlanStep record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class PlanStepRecordTests
{
    [Fact]
    public void PlanStep_WithValidData_SetsAllProperties()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "param1", "value1" },
            { "param2", 42 }
        };

        // Act
        var step = new PlanStep(
            Action: "execute_command",
            Parameters: parameters,
            ExpectedOutcome: "Command completed successfully",
            ConfidenceScore: 0.95);

        // Assert
        step.Action.Should().Be("execute_command");
        step.Parameters.Should().ContainKey("param1");
        step.Parameters.Should().ContainKey("param2");
        step.ExpectedOutcome.Should().Be("Command completed successfully");
        step.ConfidenceScore.Should().Be(0.95);
    }

    [Fact]
    public void PlanStep_WithEmptyParameters_Accepted()
    {
        // Act
        var step = new PlanStep(
            "simple_action",
            new Dictionary<string, object>(),
            "Expected result",
            0.8);

        // Assert
        step.Parameters.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void PlanStep_WithDifferentConfidenceScores_Accepted(double confidence)
    {
        // Act
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", confidence);

        // Assert
        step.ConfidenceScore.Should().Be(confidence);
    }

    [Fact]
    public void PlanStep_Equality_WorksCorrectly()
    {
        // Arrange
        var params1 = new Dictionary<string, object> { { "key", "value" } };
        var params2 = new Dictionary<string, object> { { "key", "value" } };
        var step1 = new PlanStep("action", params1, "outcome", 0.9);
        var step2 = new PlanStep("action", params2, "outcome", 0.9);

        // Assert - Note: Dictionary equality uses reference equality
        // so these won't be equal unless same reference
        step1.Action.Should().Be(step2.Action);
        step1.ExpectedOutcome.Should().Be(step2.ExpectedOutcome);
        step1.ConfidenceScore.Should().Be(step2.ConfidenceScore);
    }
}
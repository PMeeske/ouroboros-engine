using Ouroboros.Agent.MetaAI;
using PlanStep = Ouroboros.Agent.MetaAI.PlanStep;

namespace Ouroboros.Tests.Database.Storage;

/// <summary>
/// Unit tests for StepResult record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class StepResultRecordTests
{
    private static PlanStep CreateTestStep() => new PlanStep(
        "test_action",
        new Dictionary<string, object>(),
        "expected_outcome",
        0.85);

    [Fact]
    public void StepResult_WithSuccessfulStep_SetsAllProperties()
    {
        // Arrange
        var step = CreateTestStep();

        // Act
        var result = new StepResult(
            Step: step,
            Success: true,
            Output: "Step completed successfully",
            Error: null,
            Duration: TimeSpan.FromMilliseconds(500),
            ObservedState: new Dictionary<string, object> { { "state_key", "state_value" } });

        // Assert
        result.Step.Should().Be(step);
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Step completed successfully");
        result.Error.Should().BeNull();
        result.Duration.Should().Be(TimeSpan.FromMilliseconds(500));
        result.ObservedState.Should().ContainKey("state_key");
    }

    [Fact]
    public void StepResult_WithFailedStep_ContainsError()
    {
        // Act
        var result = new StepResult(
            CreateTestStep(),
            Success: false,
            Output: string.Empty,
            Error: "Connection refused",
            Duration: TimeSpan.FromSeconds(1),
            ObservedState: new Dictionary<string, object>());

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Connection refused");
    }
}
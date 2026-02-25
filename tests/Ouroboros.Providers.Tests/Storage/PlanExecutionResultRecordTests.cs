using Ouroboros.Agent.MetaAI;
using Plan = Ouroboros.Agent.MetaAI.Plan;
using PlanStep = Ouroboros.Agent.MetaAI.PlanStep;

namespace Ouroboros.Tests.Database.Storage;

/// <summary>
/// Unit tests for PlanExecutionResult record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class PlanExecutionResultRecordTests
{
    private static Plan CreateTestPlan() => new Plan(
        Goal: "Test goal",
        Steps: new List<PlanStep>
        {
            new PlanStep("step1", new Dictionary<string, object>(), "outcome1", 0.9)
        },
        ConfidenceScores: new Dictionary<string, double> { { "overall", 0.9 } },
        CreatedAt: DateTime.UtcNow);

    private static List<StepResult> CreateTestStepResults() => new List<StepResult>
    {
        new StepResult(
            new PlanStep("step1", new Dictionary<string, object>(), "outcome1", 0.9),
            Success: true,
            Output: "Step completed",
            Error: null,
            Duration: TimeSpan.FromSeconds(1),
            ObservedState: new Dictionary<string, object>())
    };

    [Fact]
    public void PlanExecutionResult_WithSuccessfulExecution_SetsAllProperties()
    {
        // Arrange
        var plan = CreateTestPlan();
        var stepResults = CreateTestStepResults();

        // Act
        var result = new PlanExecutionResult(
            Plan: plan,
            StepResults: stepResults,
            Success: true,
            FinalOutput: "Execution completed successfully",
            Metadata: new Dictionary<string, object> { { "key", "value" } },
            Duration: TimeSpan.FromSeconds(5));

        // Assert
        result.Plan.Should().Be(plan);
        result.StepResults.Should().HaveCount(1);
        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Execution completed successfully");
        result.Metadata.Should().ContainKey("key");
        result.Duration.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PlanExecutionResult_WithFailedExecution_SetsSuccessFalse()
    {
        // Act
        var result = new PlanExecutionResult(
            CreateTestPlan(),
            CreateTestStepResults(),
            Success: false,
            FinalOutput: "Execution failed",
            Metadata: new Dictionary<string, object>(),
            Duration: TimeSpan.FromSeconds(2));

        // Assert
        result.Success.Should().BeFalse();
        result.FinalOutput.Should().Be("Execution failed");
    }
}
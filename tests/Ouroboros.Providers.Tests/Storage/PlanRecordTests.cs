using Plan = Ouroboros.Agent.MetaAI.Plan;
using PlanStep = Ouroboros.Agent.MetaAI.PlanStep;

namespace Ouroboros.Tests.Database.Storage;

/// <summary>
/// Unit tests for Plan record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class PlanRecordTests
{
    [Fact]
    public void Plan_WithValidData_SetsAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var steps = new List<PlanStep>
        {
            new PlanStep("step1", new Dictionary<string, object>(), "outcome1", 0.9),
            new PlanStep("step2", new Dictionary<string, object>(), "outcome2", 0.8)
        };
        var confidenceScores = new Dictionary<string, double>
        {
            { "overall", 0.85 },
            { "step1", 0.9 },
            { "step2", 0.8 }
        };

        // Act
        var plan = new Plan(
            Goal: "Accomplish the test objective",
            Steps: steps,
            ConfidenceScores: confidenceScores,
            CreatedAt: now);

        // Assert
        plan.Goal.Should().Be("Accomplish the test objective");
        plan.Steps.Should().HaveCount(2);
        plan.ConfidenceScores.Should().HaveCount(3);
        plan.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void Plan_WithEmptySteps_Accepted()
    {
        // Act
        var plan = new Plan(
            "Empty plan",
            new List<PlanStep>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        // Assert
        plan.Steps.Should().BeEmpty();
        plan.ConfidenceScores.Should().BeEmpty();
    }
}
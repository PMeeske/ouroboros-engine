using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Plan = Ouroboros.Agent.MetaAI.Plan;
using PlanStep = Ouroboros.Agent.MetaAI.PlanStep;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class AdaptationActionTests
{
    [Fact]
    public void Constructor_WithRequiredOnly_ShouldSetDefaultOptionals()
    {
        // Arrange & Act
        var sut = new AdaptationAction(AdaptationStrategy.Retry, "Transient failure");

        // Assert
        sut.Strategy.Should().Be(AdaptationStrategy.Retry);
        sut.Reason.Should().Be("Transient failure");
        sut.RevisedPlan.Should().BeNull();
        sut.ReplacementStep.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllParameters_ShouldSetAllProperties()
    {
        // Arrange
        var plan = new Plan(
            "Revised goal",
            new List<PlanStep>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        var step = new PlanStep("replacement", new Dictionary<string, object>(), "done", 0.9);

        // Act
        var sut = new AdaptationAction(
            AdaptationStrategy.Replan,
            "Major failure",
            RevisedPlan: plan,
            ReplacementStep: step);

        // Assert
        sut.Strategy.Should().Be(AdaptationStrategy.Replan);
        sut.Reason.Should().Be("Major failure");
        sut.RevisedPlan.Should().NotBeNull();
        sut.ReplacementStep.Should().NotBeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var a = new AdaptationAction(AdaptationStrategy.Abort, "Unrecoverable");
        var b = new AdaptationAction(AdaptationStrategy.Abort, "Unrecoverable");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new AdaptationAction(AdaptationStrategy.Retry, "Timeout");

        // Act
        var modified = original with { Strategy = AdaptationStrategy.Abort };

        // Assert
        modified.Strategy.Should().Be(AdaptationStrategy.Abort);
        modified.Reason.Should().Be("Timeout");
    }
}

using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class AdaptationTriggerTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        Func<PlanExecutionContext, bool> condition = _ => true;

        // Act
        var sut = new AdaptationTrigger("HighLatency", condition, AdaptationStrategy.Retry);

        // Assert
        sut.Name.Should().Be("HighLatency");
        sut.Condition.Should().BeSameAs(condition);
        sut.Strategy.Should().Be(AdaptationStrategy.Retry);
    }

    [Fact]
    public void Constructor_WithDifferentStrategies_ShouldWork()
    {
        // Arrange
        Func<PlanExecutionContext, bool> condition = _ => false;

        // Act
        var sut = new AdaptationTrigger("Failure", condition, AdaptationStrategy.Replan);

        // Assert
        sut.Strategy.Should().Be(AdaptationStrategy.Replan);
    }

    [Fact]
    public void RecordEquality_SameConditionReference_ShouldBeEqual()
    {
        // Arrange
        Func<PlanExecutionContext, bool> condition = _ => true;
        var a = new AdaptationTrigger("T", condition, AdaptationStrategy.Abort);
        var b = new AdaptationTrigger("T", condition, AdaptationStrategy.Abort);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        Func<PlanExecutionContext, bool> condition = _ => true;
        var original = new AdaptationTrigger("Original", condition, AdaptationStrategy.Retry);

        // Act
        var modified = original with { Strategy = AdaptationStrategy.Abort };

        // Assert
        modified.Strategy.Should().Be(AdaptationStrategy.Abort);
        modified.Name.Should().Be("Original");
    }
}

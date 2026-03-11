using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class CostAwareRoutingConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var sut = new CostAwareRoutingConfig();

        // Assert
        sut.MaxCostPerPlan.Should().Be(1.0);
        sut.MinAcceptableQuality.Should().Be(0.7);
        sut.Strategy.Should().Be(CostOptimizationStrategy.Balanced);
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange & Act
        var sut = new CostAwareRoutingConfig(
            MaxCostPerPlan: 5.0,
            MinAcceptableQuality: 0.9,
            Strategy: CostOptimizationStrategy.MinimizeCost);

        // Assert
        sut.MaxCostPerPlan.Should().Be(5.0);
        sut.MinAcceptableQuality.Should().Be(0.9);
        sut.Strategy.Should().Be(CostOptimizationStrategy.MinimizeCost);
    }

    [Fact]
    public void RecordEquality_SameDefaults_ShouldBeEqual()
    {
        // Arrange
        var a = new CostAwareRoutingConfig();
        var b = new CostAwareRoutingConfig();

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new CostAwareRoutingConfig();

        // Act
        var modified = original with { MaxCostPerPlan = 10.0 };

        // Assert
        modified.MaxCostPerPlan.Should().Be(10.0);
        modified.MinAcceptableQuality.Should().Be(0.7);
    }
}

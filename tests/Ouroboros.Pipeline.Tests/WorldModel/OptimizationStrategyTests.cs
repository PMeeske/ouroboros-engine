using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class OptimizationStrategyTests
{
    [Fact]
    public void HasCostValue()
    {
        OptimizationStrategy.Cost.Should().BeDefined();
    }

    [Fact]
    public void HasSpeedValue()
    {
        OptimizationStrategy.Speed.Should().BeDefined();
    }

    [Fact]
    public void HasQualityValue()
    {
        OptimizationStrategy.Quality.Should().BeDefined();
    }

    [Fact]
    public void HasBalancedValue()
    {
        OptimizationStrategy.Balanced.Should().BeDefined();
    }

    [Fact]
    public void HasFourValues()
    {
        Enum.GetValues<OptimizationStrategy>().Should().HaveCount(4);
    }
}

namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class OptimizationStrategyTests
{
    [Theory]
    [InlineData(OptimizationStrategy.Cost, 0)]
    [InlineData(OptimizationStrategy.Speed, 1)]
    [InlineData(OptimizationStrategy.Quality, 2)]
    [InlineData(OptimizationStrategy.Balanced, 3)]
    public void EnumValues_AreDefinedCorrectly(OptimizationStrategy value, int expectedInt)
    {
        ((int)value).Should().Be(expectedInt);
    }
}

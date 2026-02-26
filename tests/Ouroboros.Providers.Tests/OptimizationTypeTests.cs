using Ouroboros.Providers;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class OptimizationTypeTests
{
    [Fact]
    public void Enum_HasFiveMembers()
    {
        Enum.GetValues<OptimizationType>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(OptimizationType.IncreasePriority, 0)]
    [InlineData(OptimizationType.ReduceUsage, 1)]
    [InlineData(OptimizationType.ConsiderRemoving, 2)]
    [InlineData(OptimizationType.AdjustParameters, 3)]
    [InlineData(OptimizationType.AddFallback, 4)]
    public void Enum_HasExpectedValues(OptimizationType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}

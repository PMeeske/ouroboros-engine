namespace Ouroboros.Tests.Routing;

[Trait("Category", "Unit")]
public sealed class TaskDetectionStrategyTests
{
    [Fact]
    public void Enum_HasThreeMembers()
    {
        Enum.GetValues<TaskDetectionStrategy>().Should().HaveCount(3);
    }

    [Theory]
    [InlineData(TaskDetectionStrategy.Heuristic, 0)]
    [InlineData(TaskDetectionStrategy.RuleBased, 1)]
    [InlineData(TaskDetectionStrategy.Hybrid, 2)]
    public void Enum_HasExpectedValues(TaskDetectionStrategy strategy, int expected)
    {
        ((int)strategy).Should().Be(expected);
    }
}

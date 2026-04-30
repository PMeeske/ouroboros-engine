namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ElectionStrategyTests
{
    [Fact]
    public void Enum_HasSevenMembers()
    {
        Enum.GetValues<ElectionStrategy>().Should().HaveCount(7);
    }

    [Theory]
    [InlineData(ElectionStrategy.Majority, 0)]
    [InlineData(ElectionStrategy.BordaCount, 2)]
    [InlineData(ElectionStrategy.MasterDecision, 6)]
    public void Enum_HasExpectedValues(ElectionStrategy strategy, int expected)
    {
        ((int)strategy).Should().Be(expected);
    }
}

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class CollectiveThinkingModeTests
{
    [Fact]
    public void Enum_HasFiveMembers()
    {
        Enum.GetValues<CollectiveThinkingMode>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(CollectiveThinkingMode.Racing, 0)]
    [InlineData(CollectiveThinkingMode.Sequential, 1)]
    [InlineData(CollectiveThinkingMode.Ensemble, 2)]
    [InlineData(CollectiveThinkingMode.Adaptive, 3)]
    [InlineData(CollectiveThinkingMode.Decomposed, 4)]
    public void Enum_HasExpectedValues(CollectiveThinkingMode mode, int expected)
    {
        ((int)mode).Should().Be(expected);
    }
}

namespace Ouroboros.Tests.Persistence;

[Trait("Category", "Unit")]
public sealed class WalEntryTypeTests
{
    [Fact]
    public void Enum_HasTwoMembers()
    {
        Enum.GetValues<WalEntryType>().Should().HaveCount(2);
    }

    [Theory]
    [InlineData(WalEntryType.AddNode, 0)]
    [InlineData(WalEntryType.AddEdge, 1)]
    public void Enum_HasExpectedValues(WalEntryType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}

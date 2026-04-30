namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ConsciousnessEventTypeTests
{
    [Fact]
    public void Enum_HasEightMembers()
    {
        Enum.GetValues<ConsciousnessEventType>().Should().HaveCount(8);
    }

    [Theory]
    [InlineData(ConsciousnessEventType.StateUpdate, 0)]
    [InlineData(ConsciousnessEventType.AttentionShift, 1)]
    [InlineData(ConsciousnessEventType.PathwayActivation, 2)]
    [InlineData(ConsciousnessEventType.Emergence, 5)]
    [InlineData(ConsciousnessEventType.Optimization, 7)]
    public void Enum_HasExpectedValues(ConsciousnessEventType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}

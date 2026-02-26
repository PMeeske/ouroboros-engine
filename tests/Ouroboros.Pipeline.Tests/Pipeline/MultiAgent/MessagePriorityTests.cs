namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class MessagePriorityTests
{
    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<MessagePriority>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(MessagePriority.Low, 0)]
    [InlineData(MessagePriority.Normal, 1)]
    [InlineData(MessagePriority.High, 2)]
    [InlineData(MessagePriority.Critical, 3)]
    public void EnumValues_AreDefinedCorrectly(MessagePriority value, int expectedInt)
    {
        ((int)value).Should().Be(expectedInt);
    }
}

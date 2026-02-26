namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class MessageTypeTests
{
    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<MessageType>().Should().HaveCount(5);
    }
}

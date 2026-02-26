namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class AgentStatusTests
{
    [Theory]
    [InlineData(AgentStatus.Idle, 0)]
    [InlineData(AgentStatus.Busy, 1)]
    [InlineData(AgentStatus.Waiting, 2)]
    [InlineData(AgentStatus.Error, 3)]
    [InlineData(AgentStatus.Offline, 4)]
    public void EnumValues_AreDefinedCorrectly(AgentStatus value, int expectedInt)
    {
        ((int)value).Should().Be(expectedInt);
    }
}

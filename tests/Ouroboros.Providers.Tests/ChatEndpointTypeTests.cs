using Ouroboros.Providers;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ChatEndpointTypeTests
{
    [Fact]
    public void Enum_HasExpectedCount()
    {
        Enum.GetValues<ChatEndpointType>().Length.Should().BeGreaterThanOrEqualTo(17);
    }

    [Theory]
    [InlineData(ChatEndpointType.Auto, 0)]
    [InlineData(ChatEndpointType.OpenAI, 2)]
    [InlineData(ChatEndpointType.Anthropic, 7)]
    public void Enum_HasExpectedValues(ChatEndpointType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}

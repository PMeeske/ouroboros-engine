namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class CausalNodeTypeTests
{
    [Theory]
    [InlineData(CausalNodeType.State, 0)]
    [InlineData(CausalNodeType.Action, 1)]
    [InlineData(CausalNodeType.Event, 2)]
    public void EnumValues_AreDefinedCorrectly(CausalNodeType value, int expectedInt)
    {
        ((int)value).Should().Be(expectedInt);
    }
}

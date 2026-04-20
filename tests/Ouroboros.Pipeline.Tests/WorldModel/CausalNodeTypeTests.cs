using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class CausalNodeTypeTests
{
    [Fact]
    public void CausalNodeType_HasStateValue()
    {
        CausalNodeType.State.Should().BeDefined();
    }

    [Fact]
    public void CausalNodeType_HasActionValue()
    {
        CausalNodeType.Action.Should().BeDefined();
    }

    [Fact]
    public void CausalNodeType_HasEventValue()
    {
        CausalNodeType.Event.Should().BeDefined();
    }

    [Fact]
    public void CausalNodeType_HasThreeValues()
    {
        Enum.GetValues<CausalNodeType>().Should().HaveCount(3);
    }

    [Fact]
    public void CausalNodeType_ValuesAreDistinct()
    {
        var values = Enum.GetValues<CausalNodeType>();
        values.Should().OnlyHaveUniqueItems();
    }
}

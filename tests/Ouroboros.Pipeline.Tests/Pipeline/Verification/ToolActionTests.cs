namespace Ouroboros.Tests.Pipeline.Verification;

using Ouroboros.Pipeline.Verification;

[Trait("Category", "Unit")]
public class ToolActionTests
{
    [Fact]
    public void Constructor_SetsToolNameAndArguments()
    {
        var action = new ToolAction("search", "query=test");

        action.ToolName.Should().Be("search");
        action.Arguments.Should().Be("query=test");
    }

    [Fact]
    public void Arguments_DefaultsToNull()
    {
        var action = new ToolAction("calculator");
        action.Arguments.Should().BeNull();
    }

    [Fact]
    public void ToMeTTaAtom_ContainsToolName()
    {
        var action = new ToolAction("search");
        action.ToMeTTaAtom().Should().Be("(ToolAction \"search\")");
    }
}

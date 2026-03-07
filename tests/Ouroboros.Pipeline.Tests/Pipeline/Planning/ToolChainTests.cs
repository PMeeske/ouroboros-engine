namespace Ouroboros.Tests.Pipeline.Planning;

using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class ToolChainTests
{
    [Fact]
    public void Empty_HasNoTools()
    {
        var chain = ToolChain.Empty;

        chain.Tools.Should().BeEmpty();
        chain.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Constructor_SetsTools()
    {
        var tools = new List<string> { "search", "summarize" };
        var chain = new ToolChain(tools);

        chain.Tools.Should().HaveCount(2);
        chain.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var tools = new List<string> { "a", "b" };
        var c1 = new ToolChain(tools);
        var c2 = new ToolChain(tools);

        c1.Should().Be(c2);
    }
}

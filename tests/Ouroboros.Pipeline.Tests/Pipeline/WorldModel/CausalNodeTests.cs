namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class CausalNodeTests
{
    [Fact]
    public void Create_SetsPropertiesWithGeneratedId()
    {
        var node = CausalNode.Create("Temperature", "Ambient temperature", CausalNodeType.State);

        node.Name.Should().Be("Temperature");
        node.Description.Should().Be("Ambient temperature");
        node.NodeType.Should().Be(CausalNodeType.State);
        node.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateState_CreatesStateNode()
    {
        var node = CausalNode.CreateState("temp", "desc");
        node.NodeType.Should().Be(CausalNodeType.State);
    }

    [Fact]
    public void CreateAction_CreatesActionNode()
    {
        var node = CausalNode.CreateAction("move", "desc");
        node.NodeType.Should().Be(CausalNodeType.Action);
    }

    [Fact]
    public void CreateEvent_CreatesEventNode()
    {
        var node = CausalNode.CreateEvent("alarm", "desc");
        node.NodeType.Should().Be(CausalNodeType.Event);
    }

    [Fact]
    public void Create_ThrowsOnNullName()
    {
        var act = () => CausalNode.Create(null!, "desc", CausalNodeType.State);
        act.Should().Throw<ArgumentNullException>();
    }
}

using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class CausalNodeTests
{
    [Fact]
    public void Create_WithValidParams_CreatesNode()
    {
        // Act
        var node = CausalNode.Create("test", "A test node", CausalNodeType.State);

        // Assert
        node.Name.Should().Be("test");
        node.Description.Should().Be("A test node");
        node.NodeType.Should().Be(CausalNodeType.State);
        node.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_NullName_ThrowsArgumentNullException()
    {
        var act = () => CausalNode.Create(null!, "desc", CausalNodeType.State);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_NullDescription_ThrowsArgumentNullException()
    {
        var act = () => CausalNode.Create("name", null!, CausalNodeType.State);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateState_CreatesNodeWithStateType()
    {
        // Act
        var node = CausalNode.CreateState("idle", "System is idle");

        // Assert
        node.NodeType.Should().Be(CausalNodeType.State);
        node.Name.Should().Be("idle");
    }

    [Fact]
    public void CreateAction_CreatesNodeWithActionType()
    {
        // Act
        var node = CausalNode.CreateAction("restart", "Restart the system");

        // Assert
        node.NodeType.Should().Be(CausalNodeType.Action);
        node.Name.Should().Be("restart");
    }

    [Fact]
    public void CreateEvent_CreatesNodeWithEventType()
    {
        // Act
        var node = CausalNode.CreateEvent("crash", "System crash occurred");

        // Assert
        node.NodeType.Should().Be(CausalNodeType.Event);
        node.Name.Should().Be("crash");
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Act
        var node1 = CausalNode.Create("a", "desc", CausalNodeType.State);
        var node2 = CausalNode.Create("b", "desc", CausalNodeType.State);

        // Assert
        node1.Id.Should().NotBe(node2.Id);
    }
}

using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class CausalPathTests
{
    [Fact]
    public void FromNode_CreatesPathWithSingleNode()
    {
        // Arrange
        var node = CausalNode.CreateState("start", "Starting state");

        // Act
        var path = CausalPath.FromNode(node);

        // Assert
        path.Nodes.Should().HaveCount(1);
        path.Nodes[0].Should().Be(node);
        path.Edges.Should().BeEmpty();
        path.TotalStrength.Should().Be(1.0);
        path.Length.Should().Be(0);
    }

    [Fact]
    public void FromNode_NullNode_ThrowsArgumentNullException()
    {
        var act = () => CausalPath.FromNode(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Extend_AddsNodeAndEdge()
    {
        // Arrange
        var node1 = CausalNode.CreateState("start", "Start");
        var node2 = CausalNode.CreateAction("action", "Action");
        var edge = CausalEdge.Create(node1.Id, node2.Id, 0.8);
        var path = CausalPath.FromNode(node1);

        // Act
        var extended = path.Extend(node2, edge);

        // Assert
        extended.Nodes.Should().HaveCount(2);
        extended.Edges.Should().HaveCount(1);
        extended.Length.Should().Be(1);
        extended.TotalStrength.Should().Be(0.8);
    }

    [Fact]
    public void Extend_MultipleTimes_MultipliesStrengths()
    {
        // Arrange
        var node1 = CausalNode.CreateState("n1", "Node 1");
        var node2 = CausalNode.CreateAction("n2", "Node 2");
        var node3 = CausalNode.CreateEvent("n3", "Node 3");
        var edge1 = CausalEdge.Create(node1.Id, node2.Id, 0.5);
        var edge2 = CausalEdge.Create(node2.Id, node3.Id, 0.6);

        var path = CausalPath.FromNode(node1)
            .Extend(node2, edge1)
            .Extend(node3, edge2);

        // Assert
        path.Nodes.Should().HaveCount(3);
        path.Edges.Should().HaveCount(2);
        path.Length.Should().Be(2);
        path.TotalStrength.Should().BeApproximately(0.3, 0.001);
    }

    [Fact]
    public void Extend_NullNode_ThrowsArgumentNullException()
    {
        // Arrange
        var path = CausalPath.FromNode(CausalNode.CreateState("s", "s"));

        // Act
        var act = () => path.Extend(null!, CausalEdge.Create(Guid.NewGuid(), Guid.NewGuid(), 0.5));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Extend_NullEdge_ThrowsArgumentNullException()
    {
        // Arrange
        var path = CausalPath.FromNode(CausalNode.CreateState("s", "s"));

        // Act
        var act = () => path.Extend(CausalNode.CreateState("t", "t"), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Extend_IsImmutable_OriginalPathUnchanged()
    {
        // Arrange
        var node1 = CausalNode.CreateState("n1", "Node 1");
        var node2 = CausalNode.CreateAction("n2", "Node 2");
        var edge = CausalEdge.Create(node1.Id, node2.Id, 0.7);
        var original = CausalPath.FromNode(node1);

        // Act
        var extended = original.Extend(node2, edge);

        // Assert
        original.Nodes.Should().HaveCount(1);
        extended.Nodes.Should().HaveCount(2);
    }
}

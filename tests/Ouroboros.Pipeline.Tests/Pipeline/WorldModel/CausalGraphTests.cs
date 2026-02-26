namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class CausalGraphTests
{
    private static CausalNode CreateNode(string name, CausalNodeType type = CausalNodeType.State)
        => CausalNode.Create(name, $"{name} description", type);

    [Fact]
    public void Empty_HasNoNodesOrEdges()
    {
        var graph = CausalGraph.Empty();

        graph.NodeCount.Should().Be(0);
        graph.EdgeCount.Should().Be(0);
    }

    [Fact]
    public void AddNode_IncreasesNodeCount()
    {
        var node = CreateNode("A");
        var result = CausalGraph.Empty().AddNode(node);

        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(1);
    }

    [Fact]
    public void AddNode_FailsForDuplicate()
    {
        var node = CreateNode("A");
        var graph = CausalGraph.Empty().AddNode(node).Value;
        var result = graph.AddNode(node);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddEdge_IncreasesEdgeCount()
    {
        var nodeA = CreateNode("A");
        var nodeB = CreateNode("B");
        var edge = CausalEdge.Create(nodeA.Id, nodeB.Id, 0.8);

        var graph = CausalGraph.Empty()
            .AddNode(nodeA).Value
            .AddNode(nodeB).Value
            .AddEdge(edge);

        graph.IsSuccess.Should().BeTrue();
        graph.Value.EdgeCount.Should().Be(1);
    }

    [Fact]
    public void AddEdge_FailsWhenSourceNodeMissing()
    {
        var nodeB = CreateNode("B");
        var edge = CausalEdge.Create(Guid.NewGuid(), nodeB.Id, 0.5);

        var graph = CausalGraph.Empty().AddNode(nodeB).Value;
        var result = graph.AddEdge(edge);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RemoveNode_RemovesNodeAndConnectedEdges()
    {
        var nodeA = CreateNode("A");
        var nodeB = CreateNode("B");
        var edge = CausalEdge.Create(nodeA.Id, nodeB.Id, 0.8);

        var graph = CausalGraph.Empty()
            .AddNode(nodeA).Value
            .AddNode(nodeB).Value
            .AddEdge(edge).Value;

        var result = graph.RemoveNode(nodeA.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(1);
        result.Value.EdgeCount.Should().Be(0);
    }

    [Fact]
    public void GetNode_ReturnsNodeWhenExists()
    {
        var node = CreateNode("A");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        var result = graph.GetNode(node.Id);
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetNode_ReturnsNoneWhenNotExists()
    {
        var result = CausalGraph.Empty().GetNode(Guid.NewGuid());
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetNodeByName_FindsNodeCaseInsensitive()
    {
        var node = CreateNode("TestNode");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        var result = graph.GetNodeByName("testnode");
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetCauses_ReturnsDirectPredecessors()
    {
        var a = CreateNode("A");
        var b = CreateNode("B");
        var edge = CausalEdge.Create(a.Id, b.Id, 0.8);

        var graph = CausalGraph.Empty()
            .AddNode(a).Value
            .AddNode(b).Value
            .AddEdge(edge).Value;

        var causes = graph.GetCauses(b.Id);
        causes.Should().HaveCount(1);
        causes[0].Name.Should().Be("A");
    }

    [Fact]
    public void GetEffects_ReturnsDirectSuccessors()
    {
        var a = CreateNode("A");
        var b = CreateNode("B");
        var edge = CausalEdge.Create(a.Id, b.Id, 0.8);

        var graph = CausalGraph.Empty()
            .AddNode(a).Value
            .AddNode(b).Value
            .AddEdge(edge).Value;

        var effects = graph.GetEffects(a.Id);
        effects.Should().HaveCount(1);
        effects[0].Name.Should().Be("B");
    }

    [Fact]
    public void FindPath_ReturnsPathBetweenConnectedNodes()
    {
        var a = CreateNode("A");
        var b = CreateNode("B");
        var c = CreateNode("C");

        var graph = CausalGraph.Empty()
            .AddNode(a).Value
            .AddNode(b).Value
            .AddNode(c).Value
            .AddEdge(CausalEdge.Create(a.Id, b.Id, 0.8)).Value
            .AddEdge(CausalEdge.Create(b.Id, c.Id, 0.7)).Value;

        var path = graph.FindPath(a.Id, c.Id);
        path.HasValue.Should().BeTrue();
    }

    [Fact]
    public void FindPath_ReturnsNoneWhenNoPath()
    {
        var a = CreateNode("A");
        var b = CreateNode("B");

        var graph = CausalGraph.Empty()
            .AddNode(a).Value
            .AddNode(b).Value;

        var path = graph.FindPath(a.Id, b.Id);
        path.HasValue.Should().BeFalse();
    }

    [Fact]
    public void HasCycle_ReturnsFalseForAcyclicGraph()
    {
        var a = CreateNode("A");
        var b = CreateNode("B");

        var graph = CausalGraph.Empty()
            .AddNode(a).Value
            .AddNode(b).Value
            .AddEdge(CausalEdge.Create(a.Id, b.Id, 0.8)).Value;

        graph.HasCycle().Should().BeFalse();
    }

    [Fact]
    public void HasCycle_ReturnsTrueForCyclicGraph()
    {
        var a = CreateNode("A");
        var b = CreateNode("B");

        var graph = CausalGraph.Empty()
            .AddNode(a).Value
            .AddNode(b).Value
            .AddEdge(CausalEdge.Create(a.Id, b.Id, 0.8)).Value
            .AddEdge(CausalEdge.Create(b.Id, a.Id, 0.6)).Value;

        graph.HasCycle().Should().BeTrue();
    }

    [Fact]
    public void GetRootNodes_ReturnsNodesWithNoIncomingEdges()
    {
        var a = CreateNode("A");
        var b = CreateNode("B");

        var graph = CausalGraph.Empty()
            .AddNode(a).Value
            .AddNode(b).Value
            .AddEdge(CausalEdge.Create(a.Id, b.Id, 0.8)).Value;

        var roots = graph.GetRootNodes();
        roots.Should().HaveCount(1);
        roots[0].Name.Should().Be("A");
    }

    [Fact]
    public void GetLeafNodes_ReturnsNodesWithNoOutgoingEdges()
    {
        var a = CreateNode("A");
        var b = CreateNode("B");

        var graph = CausalGraph.Empty()
            .AddNode(a).Value
            .AddNode(b).Value
            .AddEdge(CausalEdge.Create(a.Id, b.Id, 0.8)).Value;

        var leaves = graph.GetLeafNodes();
        leaves.Should().HaveCount(1);
        leaves[0].Name.Should().Be("B");
    }

    [Fact]
    public void GetNodesByType_FiltersCorrectly()
    {
        var action = CreateNode("A", CausalNodeType.Action);
        var state = CreateNode("S", CausalNodeType.State);

        var graph = CausalGraph.Empty()
            .AddNode(action).Value
            .AddNode(state).Value;

        graph.GetNodesByType(CausalNodeType.Action).Should().HaveCount(1);
    }

    [Fact]
    public void PredictEffects_ReturnsPredictedEffects()
    {
        var a = CreateNode("A");
        var b = CreateNode("B");

        var graph = CausalGraph.Empty()
            .AddNode(a).Value
            .AddNode(b).Value
            .AddEdge(CausalEdge.Create(a.Id, b.Id, 0.8)).Value;

        var result = graph.PredictEffects(a.Id);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public void Create_BuildsGraphFromNodesAndEdges()
    {
        var a = CreateNode("A");
        var b = CreateNode("B");
        var edge = CausalEdge.Create(a.Id, b.Id, 0.8);

        var result = CausalGraph.Create(new[] { a, b }, new[] { edge });

        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(2);
        result.Value.EdgeCount.Should().Be(1);
    }
}

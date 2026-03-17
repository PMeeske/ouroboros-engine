using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class CausalGraphTests
{
    #region Empty / Create

    [Fact]
    public void Empty_CreatesEmptyGraph()
    {
        var graph = CausalGraph.Empty();

        graph.NodeCount.Should().Be(0);
        graph.EdgeCount.Should().Be(0);
        graph.Nodes.Should().BeEmpty();
        graph.Edges.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithValidNodesAndEdges_ReturnsSuccess()
    {
        // Arrange
        var node1 = CausalNode.CreateState("s1", "State 1");
        var node2 = CausalNode.CreateAction("a1", "Action 1");
        var edge = CausalEdge.Deterministic(node1.Id, node2.Id);

        // Act
        var result = CausalGraph.Create(new[] { node1, node2 }, new[] { edge });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(2);
        result.Value.EdgeCount.Should().Be(1);
    }

    [Fact]
    public void Create_WithEdgeReferencingNonexistentNode_ReturnsFailure()
    {
        // Arrange
        var node = CausalNode.CreateState("s1", "State 1");
        var edge = CausalEdge.Deterministic(node.Id, Guid.NewGuid());

        // Act
        var result = CausalGraph.Create(new[] { node }, new[] { edge });

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region AddNode

    [Fact]
    public void AddNode_NewNode_ReturnsSuccess()
    {
        // Arrange
        var graph = CausalGraph.Empty();
        var node = CausalNode.CreateState("s", "State");

        // Act
        var result = graph.AddNode(node);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(1);
    }

    [Fact]
    public void AddNode_DuplicateId_ReturnsFailure()
    {
        // Arrange
        var node = CausalNode.CreateState("s", "State");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        // Act
        var result = graph.AddNode(node);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public void AddNode_NullNode_ThrowsArgumentNullException()
    {
        var act = () => CausalGraph.Empty().AddNode(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region AddEdge

    [Fact]
    public void AddEdge_ValidEdge_ReturnsSuccess()
    {
        // Arrange
        var node1 = CausalNode.CreateState("s", "State");
        var node2 = CausalNode.CreateAction("a", "Action");
        var graph = CausalGraph.Empty()
            .AddNode(node1).Value
            .AddNode(node2).Value;

        // Act
        var result = graph.AddEdge(CausalEdge.Create(node1.Id, node2.Id, 0.7));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EdgeCount.Should().Be(1);
    }

    [Fact]
    public void AddEdge_MissingSourceNode_ReturnsFailure()
    {
        // Arrange
        var node = CausalNode.CreateState("s", "State");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        // Act
        var result = graph.AddEdge(CausalEdge.Create(Guid.NewGuid(), node.Id, 0.5));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Source node");
    }

    [Fact]
    public void AddEdge_MissingTargetNode_ReturnsFailure()
    {
        // Arrange
        var node = CausalNode.CreateState("s", "State");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        // Act
        var result = graph.AddEdge(CausalEdge.Create(node.Id, Guid.NewGuid(), 0.5));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Target node");
    }

    #endregion

    #region RemoveNode

    [Fact]
    public void RemoveNode_ExistingNode_RemovesNodeAndEdges()
    {
        // Arrange
        var node1 = CausalNode.CreateState("s1", "State 1");
        var node2 = CausalNode.CreateAction("a1", "Action 1");
        var edge = CausalEdge.Deterministic(node1.Id, node2.Id);
        var graph = CausalGraph.Create(new[] { node1, node2 }, new[] { edge }).Value;

        // Act
        var result = graph.RemoveNode(node1.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(1);
        result.Value.EdgeCount.Should().Be(0);
    }

    [Fact]
    public void RemoveNode_NonexistentNode_ReturnsFailure()
    {
        // Act
        var result = CausalGraph.Empty().RemoveNode(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region GetNode / GetNodeByName

    [Fact]
    public void GetNode_ExistingId_ReturnsSome()
    {
        // Arrange
        var node = CausalNode.CreateState("s", "State");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        // Act
        var result = graph.GetNode(node.Id);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Name.Should().Be("s");
    }

    [Fact]
    public void GetNode_NonexistentId_ReturnsNone()
    {
        var result = CausalGraph.Empty().GetNode(Guid.NewGuid());
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetNodeByName_ExistingName_ReturnsSome()
    {
        // Arrange
        var node = CausalNode.CreateState("MyState", "A state");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        // Act
        var result = graph.GetNodeByName("mystate"); // case-insensitive

        // Assert
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetNodeByName_NonexistentName_ReturnsNone()
    {
        var result = CausalGraph.Empty().GetNodeByName("nope");
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region GetCauses / GetEffects

    [Fact]
    public void GetCauses_ReturnsDirectPredecessors()
    {
        // Arrange - A -> B -> C
        var a = CausalNode.CreateState("A", "Node A");
        var b = CausalNode.CreateAction("B", "Node B");
        var c = CausalNode.CreateEvent("C", "Node C");
        var graph = CausalGraph.Create(
            new[] { a, b, c },
            new[] {
                CausalEdge.Deterministic(a.Id, b.Id),
                CausalEdge.Deterministic(b.Id, c.Id)
            }).Value;

        // Act
        var causes = graph.GetCauses(b.Id);

        // Assert
        causes.Should().HaveCount(1);
        causes[0].Name.Should().Be("A");
    }

    [Fact]
    public void GetEffects_ReturnsDirectSuccessors()
    {
        // Arrange - A -> B, A -> C
        var a = CausalNode.CreateAction("A", "Node A");
        var b = CausalNode.CreateEvent("B", "Node B");
        var c = CausalNode.CreateState("C", "Node C");
        var graph = CausalGraph.Create(
            new[] { a, b, c },
            new[] {
                CausalEdge.Deterministic(a.Id, b.Id),
                CausalEdge.Deterministic(a.Id, c.Id)
            }).Value;

        // Act
        var effects = graph.GetEffects(a.Id);

        // Assert
        effects.Should().HaveCount(2);
    }

    [Fact]
    public void GetCauses_NoIncomingEdges_ReturnsEmpty()
    {
        var node = CausalNode.CreateState("s", "State");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        graph.GetCauses(node.Id).Should().BeEmpty();
    }

    [Fact]
    public void GetEffects_NoOutgoingEdges_ReturnsEmpty()
    {
        var node = CausalNode.CreateState("s", "State");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        graph.GetEffects(node.Id).Should().BeEmpty();
    }

    #endregion

    #region GetEdgesBetween

    [Fact]
    public void GetEdgesBetween_ExistingEdge_ReturnsEdges()
    {
        // Arrange
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var edge = CausalEdge.Create(a.Id, b.Id, 0.5);
        var graph = CausalGraph.Create(new[] { a, b }, new[] { edge }).Value;

        // Act
        var edges = graph.GetEdgesBetween(a.Id, b.Id);

        // Assert
        edges.Should().HaveCount(1);
        edges[0].Strength.Should().Be(0.5);
    }

    [Fact]
    public void GetEdgesBetween_NoEdges_ReturnsEmpty()
    {
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var graph = CausalGraph.Create(new[] { a, b }, Array.Empty<CausalEdge>()).Value;

        graph.GetEdgesBetween(a.Id, b.Id).Should().BeEmpty();
    }

    #endregion

    #region HasCycle

    [Fact]
    public void HasCycle_AcyclicGraph_ReturnsFalse()
    {
        // Arrange - A -> B -> C
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var c = CausalNode.CreateState("C", "C");
        var graph = CausalGraph.Create(
            new[] { a, b, c },
            new[] {
                CausalEdge.Deterministic(a.Id, b.Id),
                CausalEdge.Deterministic(b.Id, c.Id)
            }).Value;

        // Assert
        graph.HasCycle().Should().BeFalse();
    }

    [Fact]
    public void HasCycle_CyclicGraph_ReturnsTrue()
    {
        // Arrange - A -> B -> C -> A
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var c = CausalNode.CreateState("C", "C");
        var graph = CausalGraph.Create(
            new[] { a, b, c },
            new[] {
                CausalEdge.Deterministic(a.Id, b.Id),
                CausalEdge.Deterministic(b.Id, c.Id),
                CausalEdge.Deterministic(c.Id, a.Id)
            }).Value;

        // Assert
        graph.HasCycle().Should().BeTrue();
    }

    [Fact]
    public void HasCycle_EmptyGraph_ReturnsFalse()
    {
        CausalGraph.Empty().HasCycle().Should().BeFalse();
    }

    #endregion

    #region GetRootNodes / GetLeafNodes

    [Fact]
    public void GetRootNodes_ReturnsNodesWithNoIncoming()
    {
        // Arrange - A -> B -> C
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var c = CausalNode.CreateState("C", "C");
        var graph = CausalGraph.Create(
            new[] { a, b, c },
            new[] {
                CausalEdge.Deterministic(a.Id, b.Id),
                CausalEdge.Deterministic(b.Id, c.Id)
            }).Value;

        // Act
        var roots = graph.GetRootNodes();

        // Assert
        roots.Should().HaveCount(1);
        roots[0].Name.Should().Be("A");
    }

    [Fact]
    public void GetLeafNodes_ReturnsNodesWithNoOutgoing()
    {
        // Arrange - A -> B -> C
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var c = CausalNode.CreateState("C", "C");
        var graph = CausalGraph.Create(
            new[] { a, b, c },
            new[] {
                CausalEdge.Deterministic(a.Id, b.Id),
                CausalEdge.Deterministic(b.Id, c.Id)
            }).Value;

        // Act
        var leaves = graph.GetLeafNodes();

        // Assert
        leaves.Should().HaveCount(1);
        leaves[0].Name.Should().Be("C");
    }

    #endregion

    #region GetNodesByType

    [Fact]
    public void GetNodesByType_ReturnsOnlyMatchingType()
    {
        // Arrange
        var state = CausalNode.CreateState("s", "State");
        var action = CausalNode.CreateAction("a", "Action");
        var ev = CausalNode.CreateEvent("e", "Event");
        var graph = CausalGraph.Create(
            new[] { state, action, ev },
            Array.Empty<CausalEdge>()).Value;

        // Act
        var actions = graph.GetNodesByType(CausalNodeType.Action);

        // Assert
        actions.Should().HaveCount(1);
        actions[0].Name.Should().Be("a");
    }

    #endregion

    #region Traversal - PredictEffects

    [Fact]
    public void PredictEffects_DirectEffect_ReturnsPrediction()
    {
        // Arrange - action -> effect
        var action = CausalNode.CreateAction("act", "Action");
        var effect = CausalNode.CreateEvent("eff", "Effect");
        var edge = CausalEdge.Create(action.Id, effect.Id, 0.8);
        var graph = CausalGraph.Create(new[] { action, effect }, new[] { edge }).Value;

        // Act
        var result = graph.PredictEffects(action.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Probability.Should().Be(0.8);
    }

    [Fact]
    public void PredictEffects_TransitiveEffects_PropagatesProbability()
    {
        // Arrange - A -> B -> C with strengths 0.8 and 0.5
        var a = CausalNode.CreateAction("A", "A");
        var b = CausalNode.CreateEvent("B", "B");
        var c = CausalNode.CreateState("C", "C");
        var graph = CausalGraph.Create(
            new[] { a, b, c },
            new[] {
                CausalEdge.Create(a.Id, b.Id, 0.8),
                CausalEdge.Create(b.Id, c.Id, 0.5)
            }).Value;

        // Act
        var result = graph.PredictEffects(a.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        // B has probability 0.8, C has probability 0.4
        result.Value.Should().Contain(e => e.Node.Name == "B" && Math.Abs(e.Probability - 0.8) < 0.01);
        result.Value.Should().Contain(e => e.Node.Name == "C" && Math.Abs(e.Probability - 0.4) < 0.01);
    }

    [Fact]
    public void PredictEffects_NonexistentNode_ReturnsFailure()
    {
        var result = CausalGraph.Empty().PredictEffects(Guid.NewGuid());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void PredictEffects_NoOutgoingEdges_ReturnsEmpty()
    {
        // Arrange
        var node = CausalNode.CreateState("s", "State");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        // Act
        var result = graph.PredictEffects(node.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region Traversal - FindPath

    [Fact]
    public void FindPath_DirectPath_ReturnsPath()
    {
        // Arrange
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var edge = CausalEdge.Deterministic(a.Id, b.Id);
        var graph = CausalGraph.Create(new[] { a, b }, new[] { edge }).Value;

        // Act
        var result = graph.FindPath(a.Id, b.Id);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Nodes.Should().HaveCount(2);
        result.Value.Length.Should().Be(1);
    }

    [Fact]
    public void FindPath_SameNode_ReturnsPathWithSingleNode()
    {
        // Arrange
        var node = CausalNode.CreateState("A", "A");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        // Act
        var result = graph.FindPath(node.Id, node.Id);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Nodes.Should().HaveCount(1);
        result.Value.Length.Should().Be(0);
    }

    [Fact]
    public void FindPath_NoPath_ReturnsNone()
    {
        // Arrange - A and B are unconnected
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var graph = CausalGraph.Create(new[] { a, b }, Array.Empty<CausalEdge>()).Value;

        // Act
        var result = graph.FindPath(a.Id, b.Id);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void FindPath_NonexistentNode_ReturnsNone()
    {
        var result = CausalGraph.Empty().FindPath(Guid.NewGuid(), Guid.NewGuid());
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region Traversal - FindAllPaths

    [Fact]
    public void FindAllPaths_MultiplePathsExist_ReturnsAll()
    {
        // Arrange - A -> B, A -> C -> B (two paths from A to B)
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var c = CausalNode.CreateState("C", "C");
        var graph = CausalGraph.Create(
            new[] { a, b, c },
            new[] {
                CausalEdge.Create(a.Id, b.Id, 0.9),
                CausalEdge.Create(a.Id, c.Id, 0.8),
                CausalEdge.Create(c.Id, b.Id, 0.7)
            }).Value;

        // Act
        var paths = graph.FindAllPaths(a.Id, b.Id);

        // Assert
        paths.Should().HaveCount(2);
    }

    [Fact]
    public void FindAllPaths_NoPath_ReturnsEmpty()
    {
        // Arrange
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var graph = CausalGraph.Create(new[] { a, b }, Array.Empty<CausalEdge>()).Value;

        // Act
        var paths = graph.FindAllPaths(a.Id, b.Id);

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void FindAllPaths_SameNode_ReturnsSingleNodePath()
    {
        var node = CausalNode.CreateState("A", "A");
        var graph = CausalGraph.Empty().AddNode(node).Value;

        var paths = graph.FindAllPaths(node.Id, node.Id);

        paths.Should().HaveCount(1);
        paths[0].Length.Should().Be(0);
    }

    [Fact]
    public void FindAllPaths_OrderedByStrength_StrongestFirst()
    {
        // Arrange - A -> B (0.9), A -> C -> B (0.3 * 0.5 = 0.15)
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var c = CausalNode.CreateState("C", "C");
        var graph = CausalGraph.Create(
            new[] { a, b, c },
            new[] {
                CausalEdge.Create(a.Id, b.Id, 0.9),
                CausalEdge.Create(a.Id, c.Id, 0.3),
                CausalEdge.Create(c.Id, b.Id, 0.5)
            }).Value;

        // Act
        var paths = graph.FindAllPaths(a.Id, b.Id);

        // Assert
        paths.Should().HaveCount(2);
        paths[0].TotalStrength.Should().BeGreaterThanOrEqualTo(paths[1].TotalStrength);
    }

    #endregion

    #region Traversal - CalculateTotalCausalStrength

    [Fact]
    public void CalculateTotalCausalStrength_NoPath_ReturnsZero()
    {
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var graph = CausalGraph.Create(new[] { a, b }, Array.Empty<CausalEdge>()).Value;

        graph.CalculateTotalCausalStrength(a.Id, b.Id).Should().Be(0.0);
    }

    [Fact]
    public void CalculateTotalCausalStrength_SinglePath_ReturnsThatStrength()
    {
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var graph = CausalGraph.Create(
            new[] { a, b },
            new[] { CausalEdge.Create(a.Id, b.Id, 0.7) }).Value;

        var strength = graph.CalculateTotalCausalStrength(a.Id, b.Id);
        strength.Should().BeApproximately(0.7, 0.01);
    }

    [Fact]
    public void CalculateTotalCausalStrength_MultiplePaths_CombinesProbabilities()
    {
        // Arrange - Two independent paths, combined using 1 - product(1-p_i)
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var c = CausalNode.CreateState("C", "C");
        var graph = CausalGraph.Create(
            new[] { a, b, c },
            new[] {
                CausalEdge.Create(a.Id, b.Id, 0.5),   // direct path: 0.5
                CausalEdge.Create(a.Id, c.Id, 0.4),
                CausalEdge.Create(c.Id, b.Id, 0.5)    // indirect: 0.2
            }).Value;

        // Combined: 1 - (1-0.5)*(1-0.2) = 1 - 0.5*0.8 = 1 - 0.4 = 0.6
        var strength = graph.CalculateTotalCausalStrength(a.Id, b.Id);
        strength.Should().BeApproximately(0.6, 0.01);
    }

    #endregion

    #region CreateSubgraph

    [Fact]
    public void CreateSubgraph_ValidNodes_ReturnsSubgraph()
    {
        // Arrange
        var a = CausalNode.CreateState("A", "A");
        var b = CausalNode.CreateState("B", "B");
        var c = CausalNode.CreateState("C", "C");
        var graph = CausalGraph.Create(
            new[] { a, b, c },
            new[] {
                CausalEdge.Deterministic(a.Id, b.Id),
                CausalEdge.Deterministic(b.Id, c.Id)
            }).Value;

        // Act - subgraph with only A and B
        var result = graph.CreateSubgraph(new[] { a.Id, b.Id });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(2);
        result.Value.EdgeCount.Should().Be(1); // only A->B edge
    }

    [Fact]
    public void CreateSubgraph_NonexistentNode_ReturnsFailure()
    {
        var graph = CausalGraph.Empty();
        var result = graph.CreateSubgraph(new[] { Guid.NewGuid() });

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CreateSubgraph_NullNodeIds_ThrowsArgumentNullException()
    {
        var act = () => CausalGraph.Empty().CreateSubgraph(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}

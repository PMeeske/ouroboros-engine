namespace Ouroboros.Network.Tests;

using System.Collections.Immutable;

[Trait("Category", "Unit")]
public sealed class TransitionReplayEngineTests
{
    #region Construction

    [Fact]
    public void Constructor_NullDag_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new TransitionReplayEngine(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("dag");
    }

    #endregion

    #region ReplayPathToNode

    [Fact]
    public void ReplayPathToNode_MissingNode_ReturnsFailure()
    {
        // Arrange
        var dag = new MerkleDag();
        var engine = new TransitionReplayEngine(dag);

        // Act
        var result = engine.ReplayPathToNode(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReplayPathToNode_SingleNode_ReturnsEmptyPath()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode("Root");
        dag.AddNode(node);
        var engine = new TransitionReplayEngine(dag);

        // Act
        var result = engine.ReplayPathToNode(node.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void ReplayPathToNode_TwoNodes_ReturnsPath()
    {
        // Arrange
        var dag = new MerkleDag();
        var root = CreateNode("Root");
        var child = CreateNode("Child");
        dag.AddNode(root);
        dag.AddNode(child);
        var edge = TransitionEdge.CreateSimple(root.Id, child.Id, "Op", new { });
        dag.AddEdge(edge);
        var engine = new TransitionReplayEngine(dag);

        // Act
        var result = engine.ReplayPathToNode(child.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(edge);
    }

    [Fact]
    public void ReplayPathToNode_Chain_ReturnsOrderedPath()
    {
        // Arrange
        var dag = new MerkleDag();
        var a = CreateNode("A");
        var b = CreateNode("B");
        var c = CreateNode("C");
        dag.AddNode(a);
        dag.AddNode(b);
        dag.AddNode(c);
        var edge1 = TransitionEdge.CreateSimple(a.Id, b.Id, "Op1", new { });
        var edge2 = TransitionEdge.CreateSimple(b.Id, c.Id, "Op2", new { });
        dag.AddEdge(edge1);
        dag.AddEdge(edge2);
        var engine = new TransitionReplayEngine(dag);

        // Act
        var result = engine.ReplayPathToNode(c.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Should().Be(edge1);
        result.Value[1].Should().Be(edge2);
    }

    #endregion

    #region GetTransitionChainsByOperation

    [Fact]
    public void GetTransitionChainsByOperation_ReturnsChains()
    {
        // Arrange
        var dag = new MerkleDag();
        var a = CreateNode("A");
        var b = CreateNode("B");
        dag.AddNode(a);
        dag.AddNode(b);
        dag.AddEdge(TransitionEdge.CreateSimple(a.Id, b.Id, "Op", new { }));
        var engine = new TransitionReplayEngine(dag);

        // Act
        var chains = engine.GetTransitionChainsByOperation("Op").ToList();

        // Assert
        chains.Should().ContainSingle();
        chains[0].Should().ContainSingle();
    }

    [Fact]
    public void GetTransitionChainsByOperation_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var dag = new MerkleDag();
        var engine = new TransitionReplayEngine(dag);

        // Act
        var chains = engine.GetTransitionChainsByOperation("Missing").ToList();

        // Assert
        chains.Should().BeEmpty();
    }

    #endregion

    #region GetNodeHistory

    [Fact]
    public void GetNodeHistory_ReturnsTransitions()
    {
        // Arrange
        var dag = new MerkleDag();
        var root = CreateNode("Root");
        var child = CreateNode("Child");
        dag.AddNode(root);
        dag.AddNode(child);
        var edge = TransitionEdge.CreateSimple(root.Id, child.Id, "Op", new { });
        dag.AddEdge(edge);
        var engine = new TransitionReplayEngine(dag);

        // Act
        var history = engine.GetNodeHistory(child.Id);

        // Assert
        history.Should().ContainSingle().Which.Should().Be(edge);
    }

    [Fact]
    public void GetNodeHistory_MissingNode_ReturnsEmpty()
    {
        // Arrange
        var dag = new MerkleDag();
        var engine = new TransitionReplayEngine(dag);

        // Act
        var history = engine.GetNodeHistory(Guid.NewGuid());

        // Assert
        history.Should().BeEmpty();
    }

    #endregion

    #region QueryTransitions / QueryNodes

    [Fact]
    public void QueryTransitions_Predicate_ReturnsMatches()
    {
        // Arrange
        var dag = new MerkleDag();
        var a = CreateNode("A");
        var b = CreateNode("B");
        dag.AddNode(a);
        dag.AddNode(b);
        dag.AddEdge(TransitionEdge.CreateSimple(a.Id, b.Id, "TargetOp", new { }));
        var engine = new TransitionReplayEngine(dag);

        // Act
        var results = engine.QueryTransitions(e => e.OperationName == "TargetOp").ToList();

        // Assert
        results.Should().ContainSingle();
    }

    [Fact]
    public void QueryNodes_Predicate_ReturnsMatches()
    {
        // Arrange
        var dag = new MerkleDag();
        dag.AddNode(CreateNode("Draft"));
        dag.AddNode(CreateNode("Critique"));
        var engine = new TransitionReplayEngine(dag);

        // Act
        var results = engine.QueryNodes(n => n.TypeName == "Draft").ToList();

        // Assert
        results.Should().ContainSingle();
    }

    #endregion

    #region GetTransitionsInTimeRange / GetNodesInTimeRange

    [Fact]
    public void GetTransitionsInTimeRange_ReturnsMatches()
    {
        // Arrange
        var dag = new MerkleDag();
        var a = CreateNode("A");
        var b = CreateNode("B");
        dag.AddNode(a);
        dag.AddNode(b);
        var edge = TransitionEdge.CreateSimple(a.Id, b.Id, "Op", new { });
        dag.AddEdge(edge);
        var engine = new TransitionReplayEngine(dag);

        // Act
        var results = engine.GetTransitionsInTimeRange(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5)).ToList();

        // Assert
        results.Should().ContainSingle();
    }

    [Fact]
    public void GetNodesInTimeRange_ReturnsMatches()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode("Test");
        dag.AddNode(node);
        var engine = new TransitionReplayEngine(dag);

        // Act
        var results = engine.GetNodesInTimeRange(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5)).ToList();

        // Assert
        results.Should().ContainSingle();
    }

    #endregion

    private static MonadNode CreateNode(string typeName)
    {
        return new MonadNode(Guid.NewGuid(), typeName, "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);
    }
}

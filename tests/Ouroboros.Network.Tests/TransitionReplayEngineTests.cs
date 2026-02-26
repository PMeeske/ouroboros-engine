namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class TransitionReplayEngineTests
{
    private static MonadNode CreateNode(
        Guid? id = null,
        string typeName = "TestType",
        string payload = "{}",
        params Guid[] parentIds)
    {
        return new MonadNode(
            id ?? Guid.NewGuid(),
            typeName,
            payload,
            DateTimeOffset.UtcNow,
            parentIds.ToImmutableArray());
    }

    [Fact]
    public void Ctor_NullDag_Throws()
    {
        FluentActions.Invoking(() => new TransitionReplayEngine(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ReplayPathToNode_MissingNode_ReturnsFailure()
    {
        var dag = new MerkleDag();
        var engine = new TransitionReplayEngine(dag);

        var result = engine.ReplayPathToNode(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReplayPathToNode_RootNode_ReturnsEmptyPath()
    {
        var dag = new MerkleDag();
        var root = CreateNode();
        dag.AddNode(root);
        var engine = new TransitionReplayEngine(dag);

        var result = engine.ReplayPathToNode(root.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void ReplayPathToNode_LinearChain_ReturnsChronologicalPath()
    {
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        var n3 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        dag.AddNode(n3);
        var e1 = TransitionEdge.CreateSimple(n1.Id, n2.Id, "Step1", new { });
        var e2 = TransitionEdge.CreateSimple(n2.Id, n3.Id, "Step2", new { });
        dag.AddEdge(e1);
        dag.AddEdge(e2);

        var engine = new TransitionReplayEngine(dag);
        var result = engine.ReplayPathToNode(n3.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Id.Should().Be(e1.Id);
        result.Value[1].Id.Should().Be(e2.Id);
    }

    [Fact]
    public void GetTransitionChainsByOperation_FiltersCorrectly()
    {
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        var n3 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        dag.AddNode(n3);
        dag.AddEdge(TransitionEdge.CreateSimple(n1.Id, n2.Id, "Improve", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(n2.Id, n3.Id, "Critique", new { }));

        var engine = new TransitionReplayEngine(dag);
        var chains = engine.GetTransitionChainsByOperation("Improve").ToList();

        chains.Should().HaveCount(1);
        chains[0].Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void GetNodeHistory_RootNode_ReturnsEmpty()
    {
        var dag = new MerkleDag();
        var root = CreateNode();
        dag.AddNode(root);
        var engine = new TransitionReplayEngine(dag);

        var history = engine.GetNodeHistory(root.Id);

        history.Should().BeEmpty();
    }

    [Fact]
    public void GetNodeHistory_MissingNode_ReturnsEmpty()
    {
        var dag = new MerkleDag();
        var engine = new TransitionReplayEngine(dag);

        var history = engine.GetNodeHistory(Guid.NewGuid());

        history.Should().BeEmpty();
    }

    [Fact]
    public void QueryTransitions_WithPredicate_FiltersCorrectly()
    {
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        var n3 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        dag.AddNode(n3);
        dag.AddEdge(TransitionEdge.CreateSimple(n1.Id, n2.Id, "Improve", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(n2.Id, n3.Id, "Critique", new { }));

        var engine = new TransitionReplayEngine(dag);
        var results = engine.QueryTransitions(e => e.OperationName == "Improve").ToList();

        results.Should().HaveCount(1);
        results[0].OperationName.Should().Be("Improve");
    }

    [Fact]
    public void QueryNodes_WithPredicate_FiltersCorrectly()
    {
        var dag = new MerkleDag();
        dag.AddNode(CreateNode(typeName: "Draft"));
        dag.AddNode(CreateNode(typeName: "Critique"));
        dag.AddNode(CreateNode(typeName: "Draft"));

        var engine = new TransitionReplayEngine(dag);
        var results = engine.QueryNodes(n => n.TypeName == "Draft").ToList();

        results.Should().HaveCount(2);
    }

    [Fact]
    public void GetTransitionsInTimeRange_FiltersCorrectly()
    {
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        dag.AddEdge(TransitionEdge.CreateSimple(n1.Id, n2.Id, "Op", new { }));

        var engine = new TransitionReplayEngine(dag);
        var before = DateTimeOffset.UtcNow.AddSeconds(-10);
        var after = DateTimeOffset.UtcNow.AddSeconds(10);

        var results = engine.GetTransitionsInTimeRange(before, after).ToList();

        results.Should().HaveCount(1);
    }

    [Fact]
    public void GetNodesInTimeRange_FiltersCorrectly()
    {
        var dag = new MerkleDag();
        dag.AddNode(CreateNode());

        var engine = new TransitionReplayEngine(dag);
        var before = DateTimeOffset.UtcNow.AddSeconds(-10);
        var after = DateTimeOffset.UtcNow.AddSeconds(10);

        var results = engine.GetNodesInTimeRange(before, after).ToList();

        results.Should().HaveCount(1);
    }

    [Fact]
    public void GetNodesInTimeRange_OutOfRange_ReturnsEmpty()
    {
        var dag = new MerkleDag();
        dag.AddNode(CreateNode());

        var engine = new TransitionReplayEngine(dag);
        var future = DateTimeOffset.UtcNow.AddHours(1);
        var farFuture = DateTimeOffset.UtcNow.AddHours(2);

        var results = engine.GetNodesInTimeRange(future, farFuture).ToList();

        results.Should().BeEmpty();
    }
}

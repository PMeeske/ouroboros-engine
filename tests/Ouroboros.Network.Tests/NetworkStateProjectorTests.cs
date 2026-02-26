namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class NetworkStateProjectorTests
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
        FluentActions.Invoking(() => new NetworkStateProjector(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_ValidDag_InitializesCorrectly()
    {
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);

        projector.CurrentEpoch.Should().Be(0);
        projector.Snapshots.Should().BeEmpty();
    }

    [Fact]
    public void ProjectCurrentState_EmptyDag_ReturnsStateWithZeroCounts()
    {
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);

        var state = projector.ProjectCurrentState();

        state.TotalNodes.Should().Be(0);
        state.TotalTransitions.Should().Be(0);
    }

    [Fact]
    public void ProjectCurrentState_WithNodes_ReturnsCorrectCount()
    {
        var dag = new MerkleDag();
        dag.AddNode(CreateNode(typeName: "Draft"));
        dag.AddNode(CreateNode(typeName: "Critique"));

        var projector = new NetworkStateProjector(dag);
        var state = projector.ProjectCurrentState();

        state.TotalNodes.Should().Be(2);
    }

    [Fact]
    public void ProjectCurrentState_WithEdges_ReturnsCorrectCount()
    {
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        dag.AddEdge(TransitionEdge.CreateSimple(n1.Id, n2.Id, "TestOp", new { }));

        var projector = new NetworkStateProjector(dag);
        var state = projector.ProjectCurrentState();

        state.TotalTransitions.Should().Be(1);
    }
}

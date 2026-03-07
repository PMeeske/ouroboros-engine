namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class MerkleDagTests
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
    public void NewDag_HasZeroNodesAndEdges()
    {
        var dag = new MerkleDag();

        dag.NodeCount.Should().Be(0);
        dag.EdgeCount.Should().Be(0);
        dag.Nodes.Should().BeEmpty();
        dag.Edges.Should().BeEmpty();
    }

    [Fact]
    public void AddNode_ValidNode_ReturnsSuccess()
    {
        var dag = new MerkleDag();
        var node = CreateNode();

        var result = dag.AddNode(node);

        result.IsSuccess.Should().BeTrue();
        dag.NodeCount.Should().Be(1);
    }

    [Fact]
    public void AddNode_Null_ReturnsFailure()
    {
        var dag = new MerkleDag();

        var result = dag.AddNode(null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddNode_DuplicateId_ReturnsFailure()
    {
        var dag = new MerkleDag();
        var id = Guid.NewGuid();
        var node1 = CreateNode(id: id);
        var node2 = CreateNode(id: id);

        dag.AddNode(node1);
        var result = dag.AddNode(node2);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddNode_WithMissingParent_ReturnsFailure()
    {
        var dag = new MerkleDag();
        var missingParentId = Guid.NewGuid();
        var node = CreateNode(parentIds: missingParentId);

        var result = dag.AddNode(node);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddNode_WithExistingParent_ReturnsSuccess()
    {
        var dag = new MerkleDag();
        var parent = CreateNode();
        dag.AddNode(parent);

        var child = CreateNode(parentIds: parent.Id);
        var result = dag.AddNode(child);

        result.IsSuccess.Should().BeTrue();
        dag.NodeCount.Should().Be(2);
    }

    [Fact]
    public void AddEdge_ValidEdge_ReturnsSuccess()
    {
        var dag = new MerkleDag();
        var input = CreateNode();
        var output = CreateNode();
        dag.AddNode(input);
        dag.AddNode(output);

        var edge = TransitionEdge.CreateSimple(input.Id, output.Id, "TestOp", new { });
        var result = dag.AddEdge(edge);

        result.IsSuccess.Should().BeTrue();
        dag.EdgeCount.Should().Be(1);
    }

    [Fact]
    public void AddEdge_Null_ReturnsFailure()
    {
        var dag = new MerkleDag();

        var result = dag.AddEdge(null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddEdge_MissingInputNode_ReturnsFailure()
    {
        var dag = new MerkleDag();
        var output = CreateNode();
        dag.AddNode(output);

        var edge = TransitionEdge.CreateSimple(Guid.NewGuid(), output.Id, "TestOp", new { });
        var result = dag.AddEdge(edge);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddEdge_MissingOutputNode_ReturnsFailure()
    {
        var dag = new MerkleDag();
        var input = CreateNode();
        dag.AddNode(input);

        var edge = TransitionEdge.CreateSimple(input.Id, Guid.NewGuid(), "TestOp", new { });
        var result = dag.AddEdge(edge);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GetNode_ExistingId_ReturnsSome()
    {
        var dag = new MerkleDag();
        var node = CreateNode();
        dag.AddNode(node);

        var result = dag.GetNode(node.Id);

        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetNode_MissingId_ReturnsNone()
    {
        var dag = new MerkleDag();

        var result = dag.GetNode(Guid.NewGuid());

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetEdge_ExistingId_ReturnsSome()
    {
        var dag = new MerkleDag();
        var input = CreateNode();
        var output = CreateNode();
        dag.AddNode(input);
        dag.AddNode(output);
        var edge = TransitionEdge.CreateSimple(input.Id, output.Id, "TestOp", new { });
        dag.AddEdge(edge);

        var result = dag.GetEdge(edge.Id);

        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetEdge_MissingId_ReturnsNone()
    {
        var dag = new MerkleDag();

        var result = dag.GetEdge(Guid.NewGuid());

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetRootNodes_ReturnsNodesWithNoParents()
    {
        var dag = new MerkleDag();
        var root = CreateNode();
        dag.AddNode(root);
        var child = CreateNode(parentIds: root.Id);
        dag.AddNode(child);

        var roots = dag.GetRootNodes().ToList();

        roots.Should().ContainSingle(n => n.Id == root.Id);
    }

    [Fact]
    public void GetLeafNodes_ReturnsNodesWithNoOutgoingEdges()
    {
        var dag = new MerkleDag();
        var node1 = CreateNode();
        var node2 = CreateNode();
        dag.AddNode(node1);
        dag.AddNode(node2);
        var edge = TransitionEdge.CreateSimple(node1.Id, node2.Id, "TestOp", new { });
        dag.AddEdge(edge);

        var leaves = dag.GetLeafNodes().ToList();

        leaves.Should().ContainSingle(n => n.Id == node2.Id);
    }

    [Fact]
    public void GetIncomingEdges_ReturnsCorrectEdges()
    {
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        var edge = TransitionEdge.CreateSimple(n1.Id, n2.Id, "TestOp", new { });
        dag.AddEdge(edge);

        var incoming = dag.GetIncomingEdges(n2.Id).ToList();

        incoming.Should().HaveCount(1);
        incoming[0].Id.Should().Be(edge.Id);
    }

    [Fact]
    public void GetOutgoingEdges_ReturnsCorrectEdges()
    {
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        var edge = TransitionEdge.CreateSimple(n1.Id, n2.Id, "TestOp", new { });
        dag.AddEdge(edge);

        var outgoing = dag.GetOutgoingEdges(n1.Id).ToList();

        outgoing.Should().HaveCount(1);
        outgoing[0].Id.Should().Be(edge.Id);
    }

    [Fact]
    public void TopologicalSort_LinearChain_ReturnsCorrectOrder()
    {
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        var n3 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        dag.AddNode(n3);
        dag.AddEdge(TransitionEdge.CreateSimple(n1.Id, n2.Id, "Op1", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(n2.Id, n3.Id, "Op2", new { }));

        var result = dag.TopologicalSort();

        result.IsSuccess.Should().BeTrue();
        var sorted = result.Value;
        sorted.Should().HaveCount(3);

        // n1 must come before n2, n2 before n3
        var idxN1 = sorted.ToList().FindIndex(n => n.Id == n1.Id);
        var idxN2 = sorted.ToList().FindIndex(n => n.Id == n2.Id);
        var idxN3 = sorted.ToList().FindIndex(n => n.Id == n3.Id);
        idxN1.Should().BeLessThan(idxN2);
        idxN2.Should().BeLessThan(idxN3);
    }

    [Fact]
    public void GetNodesByType_FiltersCorrectly()
    {
        var dag = new MerkleDag();
        var draft = CreateNode(typeName: "Draft");
        var critique = CreateNode(typeName: "Critique");
        var draft2 = CreateNode(typeName: "Draft");
        dag.AddNode(draft);
        dag.AddNode(critique);
        dag.AddNode(draft2);

        var drafts = dag.GetNodesByType("Draft").ToList();

        drafts.Should().HaveCount(2);
    }

    [Fact]
    public void GetTransitionsByOperation_FiltersCorrectly()
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

        var improves = dag.GetTransitionsByOperation("Improve").ToList();

        improves.Should().HaveCount(1);
    }

    [Fact]
    public void VerifyIntegrity_ValidDag_ReturnsSuccess()
    {
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        dag.AddEdge(TransitionEdge.CreateSimple(n1.Id, n2.Id, "Op", new { }));

        var result = dag.VerifyIntegrity();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void VerifyIntegrity_EmptyDag_ReturnsSuccess()
    {
        var dag = new MerkleDag();

        var result = dag.VerifyIntegrity();

        result.IsSuccess.Should().BeTrue();
    }
}

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class MerkleDagExtensionsTests
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
    public void ToJson_EmptyDag_ReturnsValidJson()
    {
        var dag = new MerkleDag();

        var json = dag.ToJson();

        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("Nodes");
        json.Should().Contain("Edges");
    }

    [Fact]
    public void ToJson_WithNodes_SerializesNodes()
    {
        var dag = new MerkleDag();
        var node = CreateNode(typeName: "Draft");
        dag.AddNode(node);

        var json = dag.ToJson();

        json.Should().Contain("Draft");
    }

    [Fact]
    public void FromJson_ValidJson_ReturnsSuccess()
    {
        var dag = new MerkleDag();
        var n1 = CreateNode(typeName: "Draft");
        var n2 = CreateNode(typeName: "Critique");
        dag.AddNode(n1);
        dag.AddNode(n2);
        dag.AddEdge(TransitionEdge.CreateSimple(n1.Id, n2.Id, "Improve", new { }));

        var json = dag.ToJson();
        var result = MerkleDagExtensions.FromJson(json);

        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(2);
        result.Value.EdgeCount.Should().Be(1);
    }

    [Fact]
    public void FromJson_InvalidJson_ReturnsFailure()
    {
        var result = MerkleDagExtensions.FromJson("not valid json");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GetStepExecutionNodes_ReturnsStepPrefixed()
    {
        var dag = new MerkleDag();
        dag.AddNode(CreateNode(typeName: "Step:Improve"));
        dag.AddNode(CreateNode(typeName: "Step:Critique"));
        dag.AddNode(CreateNode(typeName: "Draft"));

        var steps = dag.GetStepExecutionNodes().ToList();

        steps.Should().HaveCount(2);
    }

    [Fact]
    public void GetReasoningNodes_ReturnsKnownTypes()
    {
        var dag = new MerkleDag();
        dag.AddNode(CreateNode(typeName: "Draft"));
        dag.AddNode(CreateNode(typeName: "Critique"));
        dag.AddNode(CreateNode(typeName: "Improve"));
        dag.AddNode(CreateNode(typeName: "FinalSpec"));
        dag.AddNode(CreateNode(typeName: "Other"));

        var reasoning = dag.GetReasoningNodes().ToList();

        reasoning.Should().HaveCount(4);
    }

    [Fact]
    public void GetTimeline_ReturnsSortedByCreatedAt()
    {
        var dag = new MerkleDag();
        var early = new MonadNode(
            Guid.NewGuid(), "A", "{}", DateTimeOffset.UtcNow.AddMinutes(-10),
            ImmutableArray<Guid>.Empty);
        var late = new MonadNode(
            Guid.NewGuid(), "B", "{}", DateTimeOffset.UtcNow,
            ImmutableArray<Guid>.Empty);
        dag.AddNode(late);
        dag.AddNode(early);

        var timeline = dag.GetTimeline();

        timeline[0].TypeName.Should().Be("A");
        timeline[1].TypeName.Should().Be("B");
    }

    [Fact]
    public void GetSummary_ContainsKeyInfo()
    {
        var dag = new MerkleDag();
        dag.AddNode(CreateNode(typeName: "Draft"));

        var summary = dag.GetSummary();

        summary.Should().Contain("MerkleDag Summary");
        summary.Should().Contain("Total Nodes: 1");
        summary.Should().Contain("Draft");
    }
}

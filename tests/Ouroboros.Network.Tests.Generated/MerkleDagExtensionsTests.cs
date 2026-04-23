namespace Ouroboros.Network.Tests;

using System.Collections.Immutable;

[Trait("Category", "Unit")]
public sealed class MerkleDagExtensionsTests
{
    #region ToJson / FromJson

    [Fact]
    public void ToJson_SerializesDag()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode("Test");
        dag.AddNode(node);

        // Act
        var json = dag.ToJson();

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("Nodes");
        json.Should().Contain("Edges");
    }

    [Fact]
    public void FromJson_EmptyJson_ReturnsFailure()
    {
        // Act
        var result = MerkleDagExtensions.FromJson("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void FromJson_InvalidJson_ReturnsFailure()
    {
        // Act
        var result = MerkleDagExtensions.FromJson("not-json");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void FromJson_RoundTrip_PreservesNodes()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode("RoundTrip");
        dag.AddNode(node);
        var json = dag.ToJson();

        // Act
        var result = MerkleDagExtensions.FromJson(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(1);
        result.Value.Nodes.Values.First().TypeName.Should().Be("RoundTrip");
    }

    [Fact]
    public void FromJson_RoundTrip_WithEdges_PreservesStructure()
    {
        // Arrange
        var dag = new MerkleDag();
        var root = CreateNode("Root");
        var child = new MonadNode(Guid.NewGuid(), "Child", "{}", DateTimeOffset.UtcNow, ImmutableArray.Create(root.Id));
        dag.AddNode(root);
        dag.AddNode(child);
        var edge = TransitionEdge.CreateSimple(root.Id, child.Id, "Op", new { });
        dag.AddEdge(edge);
        var json = dag.ToJson();

        // Act
        var result = MerkleDagExtensions.FromJson(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EdgeCount.Should().Be(1);
    }

    #endregion

    #region GetStepExecutionNodes

    [Fact]
    public void GetStepExecutionNodes_ReturnsStepNodes()
    {
        // Arrange
        var dag = new MerkleDag();
        var stepNode = CreateNode("Step:TokenA");
        var otherNode = CreateNode("Draft");
        dag.AddNode(stepNode);
        dag.AddNode(otherNode);

        // Act
        var steps = dag.GetStepExecutionNodes().ToList();

        // Assert
        steps.Should().ContainSingle().Which.Should().Be(stepNode);
    }

    [Fact]
    public void GetStepExecutionNodes_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var dag = new MerkleDag();
        dag.AddNode(CreateNode("Draft"));

        // Act
        var steps = dag.GetStepExecutionNodes().ToList();

        // Assert
        steps.Should().BeEmpty();
    }

    #endregion

    #region GetReasoningNodes

    [Fact]
    public void GetReasoningNodes_ReturnsReasoningNodes()
    {
        // Arrange
        var dag = new MerkleDag();
        var draft = CreateNode("Draft");
        var critique = CreateNode("Critique");
        var improve = CreateNode("Improve");
        var final = CreateNode("FinalSpec");
        var other = CreateNode("Other");
        dag.AddNode(draft);
        dag.AddNode(critique);
        dag.AddNode(improve);
        dag.AddNode(final);
        dag.AddNode(other);

        // Act
        var reasoning = dag.GetReasoningNodes().ToList();

        // Assert
        reasoning.Should().HaveCount(4);
    }

    [Fact]
    public void GetReasoningNodes_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var dag = new MerkleDag();
        dag.AddNode(CreateNode("Other"));

        // Act
        var reasoning = dag.GetReasoningNodes().ToList();

        // Assert
        reasoning.Should().BeEmpty();
    }

    #endregion

    #region GetTimeline

    [Fact]
    public void GetTimeline_ReturnsNodesOrderedByCreation()
    {
        // Arrange
        var dag = new MerkleDag();
        var node1 = new MonadNode(Guid.NewGuid(), "First", "{}", DateTimeOffset.UtcNow.AddMinutes(-10), ImmutableArray<Guid>.Empty);
        var node2 = new MonadNode(Guid.NewGuid(), "Second", "{}", DateTimeOffset.UtcNow.AddMinutes(-5), ImmutableArray<Guid>.Empty);
        var node3 = new MonadNode(Guid.NewGuid(), "Third", "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);
        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);

        // Act
        var timeline = dag.GetTimeline();

        // Assert
        timeline.Should().HaveCount(3);
        timeline[0].Should().Be(node1);
        timeline[1].Should().Be(node2);
        timeline[2].Should().Be(node3);
    }

    #endregion

    #region GetSummary

    [Fact]
    public void GetSummary_FormatsCorrectly()
    {
        // Arrange
        var dag = new MerkleDag();
        dag.AddNode(CreateNode("Draft"));
        dag.AddNode(CreateNode("Draft"));
        dag.AddNode(CreateNode("Critique"));

        // Act
        var summary = dag.GetSummary();

        // Assert
        summary.Should().Contain("MerkleDag Summary");
        summary.Should().Contain("Total Nodes: 3");
        summary.Should().Contain("Draft: 2");
        summary.Should().Contain("Critique: 1");
    }

    [Fact]
    public void GetSummary_EmptyDag_ReturnsSummary()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act
        var summary = dag.GetSummary();

        // Assert
        summary.Should().Contain("Total Nodes: 0");
        summary.Should().Contain("Root Nodes: 0");
    }

    #endregion

    #region GetOutgoingEdgeIds / GetIncomingEdgeIds

    [Fact]
    public void GetOutgoingEdgeIds_ReturnsEdgeIds()
    {
        // Arrange
        var dag = new MerkleDag();
        var input = CreateNode("Input");
        var output = CreateNode("Output");
        dag.AddNode(input);
        dag.AddNode(output);
        var edge = TransitionEdge.CreateSimple(input.Id, output.Id, "Op", new { });
        dag.AddEdge(edge);

        // Act
        var ids = dag.GetOutgoingEdgeIds(input.Id).ToList();

        // Assert
        ids.Should().ContainSingle().Which.Should().Be(edge.Id);
    }

    [Fact]
    public void GetIncomingEdgeIds_ReturnsEdgeIds()
    {
        // Arrange
        var dag = new MerkleDag();
        var input = CreateNode("Input");
        var output = CreateNode("Output");
        dag.AddNode(input);
        dag.AddNode(output);
        var edge = TransitionEdge.CreateSimple(input.Id, output.Id, "Op", new { });
        dag.AddEdge(edge);

        // Act
        var ids = dag.GetIncomingEdgeIds(output.Id).ToList();

        // Assert
        ids.Should().ContainSingle().Which.Should().Be(edge.Id);
    }

    #endregion

    #region ComputeDivergenceMap / ComputeRotationMap

    [Fact]
    public void ComputeDivergenceMap_ReturnsValuesForAllNodes()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode("Test");
        dag.AddNode(node);

        // Act
        var map = dag.ComputeDivergenceMap(_ => new float[] { 1f, 0f });

        // Assert
        map.Should().ContainKey(node.Id);
    }

    [Fact]
    public void ComputeRotationMap_ReturnsValuesForAllNodes()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode("Test");
        dag.AddNode(node);

        // Act
        var map = dag.ComputeRotationMap(_ => new float[] { 1f, 0f });

        // Assert
        map.Should().ContainKey(node.Id);
    }

    #endregion

    private static MonadNode CreateNode(string typeName)
    {
        return new MonadNode(Guid.NewGuid(), typeName, "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);
    }
}

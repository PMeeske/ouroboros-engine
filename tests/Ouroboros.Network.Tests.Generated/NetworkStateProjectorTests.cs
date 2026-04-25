namespace Ouroboros.Network.Tests;

using System.Collections.Immutable;

[Trait("Category", "Unit")]
public sealed class NetworkStateProjectorTests
{
    #region Construction

    [Fact]
    public void Constructor_NullDag_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new NetworkStateProjector(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("dag");
    }

    [Fact]
    public void Constructor_InitializesWithZeroEpoch()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act
        var projector = new NetworkStateProjector(dag);

        // Assert
        projector.CurrentEpoch.Should().Be(0);
        projector.Snapshots.Should().BeEmpty();
    }

    #endregion

    #region ProjectCurrentState

    [Fact]
    public void ProjectCurrentState_EmptyDag_ReturnsEmptyState()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);

        // Act
        var state = projector.ProjectCurrentState();

        // Assert
        state.TotalNodes.Should().Be(0);
        state.TotalTransitions.Should().Be(0);
        state.NodeCountByType.Should().BeEmpty();
        state.TransitionCountByOperation.Should().BeEmpty();
        state.AverageConfidence.Should().BeNull();
        state.TotalProcessingTimeMs.Should().BeNull();
    }

    [Fact]
    public void ProjectCurrentState_WithNodes_ReturnsCorrectCounts()
    {
        // Arrange
        var dag = new MerkleDag();
        dag.AddNode(CreateNode("Draft"));
        dag.AddNode(CreateNode("Draft"));
        dag.AddNode(CreateNode("Critique"));
        var projector = new NetworkStateProjector(dag);

        // Act
        var state = projector.ProjectCurrentState();

        // Assert
        state.TotalNodes.Should().Be(3);
        state.NodeCountByType.Should().ContainKey("Draft").WhoseValue.Should().Be(2);
        state.NodeCountByType.Should().ContainKey("Critique").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ProjectCurrentState_WithEdges_ReturnsTransitionCounts()
    {
        // Arrange
        var dag = new MerkleDag();
        var input = CreateNode("Input");
        var output = CreateNode("Output");
        dag.AddNode(input);
        dag.AddNode(output);
        dag.AddEdge(TransitionEdge.CreateSimple(input.Id, output.Id, "UseCritique", new { }, 0.9, 100L));
        var projector = new NetworkStateProjector(dag);

        // Act
        var state = projector.ProjectCurrentState();

        // Assert
        state.TotalTransitions.Should().Be(1);
        state.TransitionCountByOperation.Should().ContainKey("UseCritique").WhoseValue.Should().Be(1);
        state.AverageConfidence.Should().Be(0.9);
        state.TotalProcessingTimeMs.Should().Be(100L);
    }

    [Fact]
    public void ProjectCurrentState_MultipleEdges_AveragesConfidence()
    {
        // Arrange
        var dag = new MerkleDag();
        var a = CreateNode("A");
        var b = CreateNode("B");
        var c = CreateNode("C");
        dag.AddNode(a);
        dag.AddNode(b);
        dag.AddNode(c);
        dag.AddEdge(TransitionEdge.CreateSimple(a.Id, b.Id, "Op", new { }, 0.8, 50L));
        dag.AddEdge(TransitionEdge.CreateSimple(b.Id, c.Id, "Op", new { }, 1.0, 150L));
        var projector = new NetworkStateProjector(dag);

        // Act
        var state = projector.ProjectCurrentState();

        // Assert
        state.AverageConfidence.Should().BeApproximately(0.9, 0.001);
        state.TotalProcessingTimeMs.Should().Be(200L);
    }

    [Fact]
    public void ProjectCurrentState_WithMetadata_IncludesMetadata()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);
        var metadata = ImmutableDictionary<string, string>.Empty.Add("key", "value");

        // Act
        var state = projector.ProjectCurrentState(metadata);

        // Assert
        state.Metadata.Should().ContainKey("key").WhoseValue.Should().Be("value");
    }

    [Fact]
    public void ProjectCurrentState_IdentifiesRootAndLeafNodes()
    {
        // Arrange
        var dag = new MerkleDag();
        var root = CreateNode("Root");
        var leaf = CreateNode("Leaf");
        dag.AddNode(root);
        dag.AddNode(leaf);
        dag.AddEdge(TransitionEdge.CreateSimple(root.Id, leaf.Id, "Op", new { }));
        var projector = new NetworkStateProjector(dag);

        // Act
        var state = projector.ProjectCurrentState();

        // Assert
        state.RootNodeIds.Should().ContainSingle().Which.Should().Be(root.Id);
        state.LeafNodeIds.Should().ContainSingle().Which.Should().Be(leaf.Id);
    }

    #endregion

    #region CreateSnapshot

    [Fact]
    public void CreateSnapshot_IncrementsEpoch()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);

        // Act
        projector.CreateSnapshot();
        projector.CreateSnapshot();

        // Assert
        projector.CurrentEpoch.Should().Be(2);
        projector.Snapshots.Should().HaveCount(2);
    }

    [Fact]
    public void CreateSnapshot_StoresSnapshot()
    {
        // Arrange
        var dag = new MerkleDag();
        dag.AddNode(CreateNode("Test"));
        var projector = new NetworkStateProjector(dag);

        // Act
        var snapshot = projector.CreateSnapshot();

        // Assert
        projector.Snapshots.Should().ContainSingle().Which.Should().Be(snapshot);
        snapshot.Epoch.Should().Be(0);
    }

    #endregion

    #region GetSnapshot

    [Fact]
    public void GetSnapshot_ExistingEpoch_ReturnsSome()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);
        var snapshot = projector.CreateSnapshot();

        // Act
        var result = projector.GetSnapshot(0);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(snapshot);
    }

    [Fact]
    public void GetSnapshot_NonExistingEpoch_ReturnsNone()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);

        // Act
        var result = projector.GetSnapshot(999);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region GetLatestSnapshot

    [Fact]
    public void GetLatestSnapshot_WithSnapshots_ReturnsLast()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);
        projector.CreateSnapshot();
        var last = projector.CreateSnapshot();

        // Act
        var result = projector.GetLatestSnapshot();

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(last);
    }

    [Fact]
    public void GetLatestSnapshot_NoSnapshots_ReturnsNone()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);

        // Act
        var result = projector.GetLatestSnapshot();

        // Assert
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region ComputeDelta

    [Fact]
    public void ComputeDelta_ValidEpochs_ReturnsDelta()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);
        projector.CreateSnapshot();
        dag.AddNode(CreateNode("New"));
        projector.CreateSnapshot();

        // Act
        var result = projector.ComputeDelta(0, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeDelta.Should().Be(1);
        result.Value.TransitionDelta.Should().Be(0);
    }

    [Fact]
    public void ComputeDelta_MissingFromEpoch_ReturnsFailure()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);

        // Act
        var result = projector.ComputeDelta(0, 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("epoch 0");
    }

    [Fact]
    public void ComputeDelta_MissingToEpoch_ReturnsFailure()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);
        projector.CreateSnapshot();

        // Act
        var result = projector.ComputeDelta(0, 999);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("epoch 999");
    }

    #endregion

    private static MonadNode CreateNode(string typeName)
    {
        return new MonadNode(Guid.NewGuid(), typeName, "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);
    }
}

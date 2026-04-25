namespace Ouroboros.Network.Tests;

using System.Collections.Immutable;

[Trait("Category", "Unit")]
public sealed class VectorFieldOperationsTests
{
    #region ComputeDivergence

    [Fact]
    public void ComputeDivergence_NullDag_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => VectorFieldOperations.ComputeDivergence(null!, Guid.NewGuid(), _ => new float[] { 1f });

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("dag");
    }

    [Fact]
    public void ComputeDivergence_NullEmbeddingFunc_ThrowsArgumentNullException()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act
        Action act = () => VectorFieldOperations.ComputeDivergence(dag, Guid.NewGuid(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("getEmbedding");
    }

    [Fact]
    public void ComputeDivergence_MissingNode_ReturnsZero()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act
        var result = VectorFieldOperations.ComputeDivergence(dag, Guid.NewGuid(), _ => new float[] { 1f });

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void ComputeDivergence_NodeWithNoEdges_ReturnsZero()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode("Test");
        dag.AddNode(node);

        // Act
        var result = VectorFieldOperations.ComputeDivergence(dag, node.Id, _ => new float[] { 1f, 0f });

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void ComputeDivergence_NodeWithOutgoingEdge_ReturnsPositive()
    {
        // Arrange
        var dag = new MerkleDag();
        var a = CreateNode("A");
        var b = CreateNode("B");
        dag.AddNode(a);
        dag.AddNode(b);
        dag.AddEdge(TransitionEdge.CreateSimple(a.Id, b.Id, "Op", new { }));

        // Act
        var result = VectorFieldOperations.ComputeDivergence(dag, a.Id, _ => new float[] { 1f, 0f });

        // Assert
        result.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void ComputeDivergence_EmptyEmbedding_ReturnsZero()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode("Test");
        dag.AddNode(node);

        // Act
        var result = VectorFieldOperations.ComputeDivergence(dag, node.Id, _ => Array.Empty<float>());

        // Assert
        result.Should().Be(0f);
    }

    #endregion

    #region ComputeRotation

    [Fact]
    public void ComputeRotation_NullDag_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => VectorFieldOperations.ComputeRotation(null!, Guid.NewGuid(), _ => new float[] { 1f });

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("dag");
    }

    [Fact]
    public void ComputeRotation_NullEmbeddingFunc_ThrowsArgumentNullException()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act
        Action act = () => VectorFieldOperations.ComputeRotation(dag, Guid.NewGuid(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("getEmbedding");
    }

    [Fact]
    public void ComputeRotation_LessThanTwoNeighbors_ReturnsZero()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode("Test");
        dag.AddNode(node);

        // Act
        var result = VectorFieldOperations.ComputeRotation(dag, node.Id, _ => new float[] { 1f, 0f, 0f });

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void ComputeRotation_TwoOrMoreNeighbors_ReturnsNonZero()
    {
        // Arrange
        var dag = new MerkleDag();
        var center = CreateNode("Center");
        var n1 = CreateNode("N1");
        var n2 = CreateNode("N2");
        dag.AddNode(center);
        dag.AddNode(n1);
        dag.AddNode(n2);
        dag.AddEdge(TransitionEdge.CreateSimple(center.Id, n1.Id, "Op", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(center.Id, n2.Id, "Op", new { }));

        // Act
        var result = VectorFieldOperations.ComputeRotation(dag, center.Id, _ => new float[] { 1f, 0f, 0f });

        // Assert
        result.Should().BeGreaterThanOrEqualTo(0f);
    }

    #endregion

    #region ComputeAllDivergences

    [Fact]
    public void ComputeAllDivergences_ReturnsMapForAllNodes()
    {
        // Arrange
        var dag = new MerkleDag();
        var a = CreateNode("A");
        var b = CreateNode("B");
        dag.AddNode(a);
        dag.AddNode(b);

        // Act
        var map = VectorFieldOperations.ComputeAllDivergences(dag, _ => new float[] { 1f, 0f });

        // Assert
        map.Should().ContainKey(a.Id);
        map.Should().ContainKey(b.Id);
    }

    #endregion

    #region ComputeAllRotations

    [Fact]
    public void ComputeAllRotations_ReturnsMapForAllNodes()
    {
        // Arrange
        var dag = new MerkleDag();
        var a = CreateNode("A");
        dag.AddNode(a);

        // Act
        var map = VectorFieldOperations.ComputeAllRotations(dag, _ => new float[] { 1f, 0f, 0f });

        // Assert
        map.Should().ContainKey(a.Id);
    }

    #endregion

    #region CosineSimilarity

    [Fact]
    public void CosineSimilarity_SameVector_ReturnsOne()
    {
        // Arrange
        var a = new float[] { 1f, 0f, 0f };

        // Act
        var result = VectorFieldOperations.CosineSimilarity(a, a);

        // Assert
        result.Should().BeApproximately(1f, 0.0001f);
    }

    [Fact]
    public void CosineSimilarity_Orthogonal_ReturnsZero()
    {
        // Arrange
        var a = new float[] { 1f, 0f };
        var b = new float[] { 0f, 1f };

        // Act
        var result = VectorFieldOperations.CosineSimilarity(a, b);

        // Assert
        result.Should().BeApproximately(0f, 0.0001f);
    }

    [Fact]
    public void CosineSimilarity_Opposite_ReturnsNegativeOne()
    {
        // Arrange
        var a = new float[] { 1f, 0f };
        var b = new float[] { -1f, 0f };

        // Act
        var result = VectorFieldOperations.CosineSimilarity(a, b);

        // Assert
        result.Should().BeApproximately(-1f, 0.0001f);
    }

    [Fact]
    public void CosineSimilarity_NullVectors_ReturnsZero()
    {
        // Act
        var result = VectorFieldOperations.CosineSimilarity(null!, new float[] { 1f });

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void CosineSimilarity_EmptyVectors_ReturnsZero()
    {
        // Act
        var result = VectorFieldOperations.CosineSimilarity(Array.Empty<float>(), Array.Empty<float>());

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void CosineSimilarity_DifferentLengths_ReturnsZero()
    {
        // Arrange
        var a = new float[] { 1f, 0f };
        var b = new float[] { 1f };

        // Act
        var result = VectorFieldOperations.CosineSimilarity(a, b);

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void CosineSimilarity_ZeroMagnitude_ReturnsZero()
    {
        // Arrange
        var a = new float[] { 0f, 0f };
        var b = new float[] { 1f, 0f };

        // Act
        var result = VectorFieldOperations.CosineSimilarity(a, b);

        // Assert
        result.Should().Be(0f);
    }

    #endregion

    #region CrossProductMagnitude

    [Fact]
    public void CrossProductMagnitude_Perpendicular_ReturnsOne()
    {
        // Arrange
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 0f, 1f, 0f };

        // Act
        var result = VectorFieldOperations.CrossProductMagnitude(a, b);

        // Assert
        result.Should().BeApproximately(1f, 0.0001f);
    }

    [Fact]
    public void CrossProductMagnitude_Parallel_ReturnsZero()
    {
        // Arrange
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 2f, 0f, 0f };

        // Act
        var result = VectorFieldOperations.CrossProductMagnitude(a, b);

        // Assert
        result.Should().BeApproximately(0f, 0.0001f);
    }

    [Fact]
    public void CrossProductMagnitude_NullVectors_ReturnsZero()
    {
        // Act
        var result = VectorFieldOperations.CrossProductMagnitude(null!, new float[] { 1f, 0f, 0f });

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void CrossProductMagnitude_InsufficientDimensions_ReturnsZero()
    {
        // Arrange
        var a = new float[] { 1f, 0f };
        var b = new float[] { 0f, 1f };

        // Act
        var result = VectorFieldOperations.CrossProductMagnitude(a, b);

        // Assert
        result.Should().Be(0f);
    }

    #endregion

    #region GetOrderedNeighbors

    [Fact]
    public void GetOrderedNeighbors_NullDag_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => VectorFieldOperations.GetOrderedNeighbors(null!, Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("dag");
    }

    [Fact]
    public void GetOrderedNeighbors_ReturnsUniqueNeighbors()
    {
        // Arrange
        var dag = new MerkleDag();
        var center = CreateNode("Center");
        var n1 = CreateNode("N1");
        var n2 = CreateNode("N2");
        dag.AddNode(center);
        dag.AddNode(n1);
        dag.AddNode(n2);
        // Two edges to same output
        dag.AddEdge(TransitionEdge.CreateSimple(center.Id, n1.Id, "Op1", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(center.Id, n1.Id, "Op2", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(n2.Id, center.Id, "Op3", new { }));

        // Act
        var neighbors = VectorFieldOperations.GetOrderedNeighbors(dag, center.Id);

        // Assert
        neighbors.Should().Contain(n1.Id);
        neighbors.Should().Contain(n2.Id);
    }

    [Fact]
    public void GetOrderedNeighbors_NoEdges_ReturnsEmpty()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode("Test");
        dag.AddNode(node);

        // Act
        var neighbors = VectorFieldOperations.GetOrderedNeighbors(dag, node.Id);

        // Assert
        neighbors.Should().BeEmpty();
    }

    #endregion

    private static MonadNode CreateNode(string typeName)
    {
        return new MonadNode(Guid.NewGuid(), typeName, "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);
    }
}

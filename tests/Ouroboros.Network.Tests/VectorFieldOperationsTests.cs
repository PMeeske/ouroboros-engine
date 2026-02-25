// <copyright file="VectorFieldOperationsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Network;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using Ouroboros.Domain.States;
using Ouroboros.Network;
using Xunit;

/// <summary>
/// Tests for VectorFieldOperations discrete differential geometry computations.
/// Validates divergence and rotation calculations on various graph topologies.
/// </summary>
[Trait("Category", "Unit")]
public class VectorFieldOperationsTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        // Arrange
        var vector = new float[] { 1.0f, 0.0f, 0.0f };

        // Act
        var similarity = VectorFieldOperations.CosineSimilarity(vector, vector);

        // Assert
        similarity.Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 0.0f, 0.0f };
        var vectorB = new float[] { 0.0f, 1.0f, 0.0f };

        // Act
        var similarity = VectorFieldOperations.CosineSimilarity(vectorA, vectorB);

        // Assert
        similarity.Should().BeApproximately(0.0f, 0.001f);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 0.0f, 0.0f };
        var vectorB = new float[] { -1.0f, 0.0f, 0.0f };

        // Act
        var similarity = VectorFieldOperations.CosineSimilarity(vectorA, vectorB);

        // Assert
        similarity.Should().BeApproximately(-1.0f, 0.001f);
    }

    [Fact]
    public void CosineSimilarity_NullVectors_ReturnsZero()
    {
        // Act
        var similarity = VectorFieldOperations.CosineSimilarity(null!, null!);

        // Assert
        similarity.Should().Be(0.0f);
    }

    [Fact]
    public void CosineSimilarity_ZeroMagnitudeVector_ReturnsZero()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 0.0f, 0.0f };
        var vectorB = new float[] { 0.0f, 0.0f, 0.0f };

        // Act
        var similarity = VectorFieldOperations.CosineSimilarity(vectorA, vectorB);

        // Assert
        similarity.Should().Be(0.0f);
    }

    [Fact]
    public void CrossProductMagnitude_OrthogonalUnitVectors_ReturnsOne()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 0.0f, 0.0f };
        var vectorB = new float[] { 0.0f, 1.0f, 0.0f };

        // Act
        var magnitude = VectorFieldOperations.CrossProductMagnitude(vectorA, vectorB);

        // Assert
        magnitude.Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public void CrossProductMagnitude_ParallelVectors_ReturnsZero()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 0.0f, 0.0f };
        var vectorB = new float[] { 2.0f, 0.0f, 0.0f };

        // Act
        var magnitude = VectorFieldOperations.CrossProductMagnitude(vectorA, vectorB);

        // Assert
        magnitude.Should().BeApproximately(0.0f, 0.001f);
    }

    [Fact]
    public void ComputeDivergence_SingleNode_ReturnsZero()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = MonadNode.FromReasoningState(new Draft("Test"));
        dag.AddNode(node);

        var embeddings = new Dictionary<Guid, float[]>
        {
            [node.Id] = new float[] { 1.0f, 0.0f, 0.0f },
        };

        // Act
        var divergence = VectorFieldOperations.ComputeDivergence(
            dag,
            node.Id,
            id => embeddings[id]);

        // Assert
        divergence.Should().Be(0.0f);
    }

    [Fact]
    public void ComputeDivergence_LineGraph_PositiveForSource()
    {
        // Arrange - Create a simple line: node1 -> node2 -> node3
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("Node1"));
        var node2 = MonadNode.FromReasoningState(new Draft("Node2"));
        var node3 = MonadNode.FromReasoningState(new Draft("Node3"));

        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);

        var edge1 = TransitionEdge.CreateSimple(node1.Id, node2.Id, "Forward", new { });
        var edge2 = TransitionEdge.CreateSimple(node2.Id, node3.Id, "Forward", new { });

        dag.AddEdge(edge1);
        dag.AddEdge(edge2);

        // Embeddings: all pointing in same direction (positive flow)
        var embeddings = new Dictionary<Guid, float[]>
        {
            [node1.Id] = new float[] { 1.0f, 0.0f, 0.0f },
            [node2.Id] = new float[] { 1.0f, 0.0f, 0.0f },
            [node3.Id] = new float[] { 1.0f, 0.0f, 0.0f },
        };

        // Act
        var divergence1 = VectorFieldOperations.ComputeDivergence(
            dag,
            node1.Id,
            id => embeddings[id]);

        // Assert - node1 is a source (only outgoing edges)
        divergence1.Should().BeGreaterThan(0.0f);
    }

    [Fact]
    public void ComputeDivergence_LineGraph_NegativeForSink()
    {
        // Arrange - Create a simple line: node1 -> node2 -> node3
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("Node1"));
        var node2 = MonadNode.FromReasoningState(new Draft("Node2"));
        var node3 = MonadNode.FromReasoningState(new Draft("Node3"));

        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);

        var edge1 = TransitionEdge.CreateSimple(node1.Id, node2.Id, "Forward", new { });
        var edge2 = TransitionEdge.CreateSimple(node2.Id, node3.Id, "Forward", new { });

        dag.AddEdge(edge1);
        dag.AddEdge(edge2);

        // Embeddings: all pointing in same direction
        var embeddings = new Dictionary<Guid, float[]>
        {
            [node1.Id] = new float[] { 1.0f, 0.0f, 0.0f },
            [node2.Id] = new float[] { 1.0f, 0.0f, 0.0f },
            [node3.Id] = new float[] { 1.0f, 0.0f, 0.0f },
        };

        // Act
        var divergence3 = VectorFieldOperations.ComputeDivergence(
            dag,
            node3.Id,
            id => embeddings[id]);

        // Assert - node3 is a sink (only incoming edges)
        divergence3.Should().BeLessThan(0.0f);
    }

    [Fact]
    public void ComputeDivergence_StarGraph_CenterHasHighDivergence()
    {
        // Arrange - Create star graph: center -> spoke1, spoke2, spoke3
        var dag = new MerkleDag();
        var center = MonadNode.FromReasoningState(new Draft("Center"));
        var spoke1 = MonadNode.FromReasoningState(new Draft("Spoke1"));
        var spoke2 = MonadNode.FromReasoningState(new Draft("Spoke2"));
        var spoke3 = MonadNode.FromReasoningState(new Draft("Spoke3"));

        dag.AddNode(center);
        dag.AddNode(spoke1);
        dag.AddNode(spoke2);
        dag.AddNode(spoke3);

        dag.AddEdge(TransitionEdge.CreateSimple(center.Id, spoke1.Id, "Expand", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(center.Id, spoke2.Id, "Expand", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(center.Id, spoke3.Id, "Expand", new { }));

        // Embeddings: center and spokes have similar embeddings
        var embeddings = new Dictionary<Guid, float[]>
        {
            [center.Id] = new float[] { 1.0f, 0.0f, 0.0f },
            [spoke1.Id] = new float[] { 0.9f, 0.1f, 0.0f },
            [spoke2.Id] = new float[] { 0.9f, -0.1f, 0.0f },
            [spoke3.Id] = new float[] { 0.8f, 0.0f, 0.1f },
        };

        // Act
        var centerDivergence = VectorFieldOperations.ComputeDivergence(
            dag,
            center.Id,
            id => embeddings[id]);

        // Assert - center is a strong source
        centerDivergence.Should().BeGreaterThan(2.0f);
    }

    [Fact]
    public void ComputeRotation_CycleGraph_HasNonZeroRotation()
    {
        // Arrange - Create cycle: node1 -> node2 -> node3 -> node1
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("Node1"));
        var node2 = MonadNode.FromReasoningState(new Draft("Node2"));
        var node3 = MonadNode.FromReasoningState(new Draft("Node3"));
        var node4 = MonadNode.FromReasoningState(new Draft("Node4"));

        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);
        dag.AddNode(node4);

        // Create a DAG-compliant structure (no actual cycles in DAG)
        // but with semantic cycle pattern in embeddings
        dag.AddEdge(TransitionEdge.CreateSimple(node1.Id, node2.Id, "Forward", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(node1.Id, node3.Id, "Forward", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(node2.Id, node4.Id, "Forward", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(node3.Id, node4.Id, "Forward", new { }));

        // Embeddings arranged in a circular pattern in 3D space
        var embeddings = new Dictionary<Guid, float[]>
        {
            [node1.Id] = new float[] { 1.0f, 0.0f, 0.0f },
            [node2.Id] = new float[] { 0.0f, 1.0f, 0.0f },
            [node3.Id] = new float[] { -1.0f, 0.0f, 0.0f },
            [node4.Id] = new float[] { 0.0f, -1.0f, 0.0f },
        };

        // Act
        var rotation = VectorFieldOperations.ComputeRotation(
            dag,
            node1.Id,
            id => embeddings[id]);

        // Assert - should have non-zero rotation due to circular embedding pattern
        rotation.Should().BeGreaterThan(0.0f);
    }

    [Fact]
    public void ComputeRotation_SingleNode_ReturnsZero()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = MonadNode.FromReasoningState(new Draft("Test"));
        dag.AddNode(node);

        var embeddings = new Dictionary<Guid, float[]>
        {
            [node.Id] = new float[] { 1.0f, 0.0f, 0.0f },
        };

        // Act
        var rotation = VectorFieldOperations.ComputeRotation(
            dag,
            node.Id,
            id => embeddings[id]);

        // Assert
        rotation.Should().Be(0.0f);
    }

    [Fact]
    public void ComputeAllDivergences_EmptyGraph_ReturnsEmptyDictionary()
    {
        // Arrange
        var dag = new MerkleDag();
        Func<Guid, float[]> getEmbedding = _ => new float[] { 0.0f, 0.0f, 0.0f };

        // Act
        var divergences = VectorFieldOperations.ComputeAllDivergences(dag, getEmbedding);

        // Assert
        divergences.Should().BeEmpty();
    }

    [Fact]
    public void ComputeAllDivergences_MultipleNodes_ReturnsAllDivergences()
    {
        // Arrange
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("Node1"));
        var node2 = MonadNode.FromReasoningState(new Draft("Node2"));
        var node3 = MonadNode.FromReasoningState(new Draft("Node3"));

        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);

        dag.AddEdge(TransitionEdge.CreateSimple(node1.Id, node2.Id, "Forward", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(node2.Id, node3.Id, "Forward", new { }));

        var embeddings = new Dictionary<Guid, float[]>
        {
            [node1.Id] = new float[] { 1.0f, 0.0f, 0.0f },
            [node2.Id] = new float[] { 1.0f, 0.0f, 0.0f },
            [node3.Id] = new float[] { 1.0f, 0.0f, 0.0f },
        };

        // Act
        var divergences = VectorFieldOperations.ComputeAllDivergences(
            dag,
            id => embeddings[id]);

        // Assert
        divergences.Should().HaveCount(3);
        divergences.Should().ContainKey(node1.Id);
        divergences.Should().ContainKey(node2.Id);
        divergences.Should().ContainKey(node3.Id);
    }

    [Fact]
    public void ComputeAllRotations_EmptyGraph_ReturnsEmptyDictionary()
    {
        // Arrange
        var dag = new MerkleDag();
        Func<Guid, float[]> getEmbedding = _ => new float[] { 0.0f, 0.0f, 0.0f };

        // Act
        var rotations = VectorFieldOperations.ComputeAllRotations(dag, getEmbedding);

        // Assert
        rotations.Should().BeEmpty();
    }

    [Fact]
    public void GetOrderedNeighbors_ReturnsUniqueNeighbors()
    {
        // Arrange
        var dag = new MerkleDag();
        var center = MonadNode.FromReasoningState(new Draft("Center"));
        var neighbor1 = MonadNode.FromReasoningState(new Draft("N1"));
        var neighbor2 = MonadNode.FromReasoningState(new Draft("N2"));

        dag.AddNode(center);
        dag.AddNode(neighbor1);
        dag.AddNode(neighbor2);

        dag.AddEdge(TransitionEdge.CreateSimple(center.Id, neighbor1.Id, "Out", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(neighbor2.Id, center.Id, "In", new { }));

        // Act
        var neighbors = VectorFieldOperations.GetOrderedNeighbors(dag, center.Id);

        // Assert
        neighbors.Should().HaveCount(2);
        neighbors.Should().Contain(neighbor1.Id);
        neighbors.Should().Contain(neighbor2.Id);
    }

    [Fact]
    public void GetOrderedNeighbors_DisconnectedNode_ReturnsEmpty()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = MonadNode.FromReasoningState(new Draft("Isolated"));
        dag.AddNode(node);

        // Act
        var neighbors = VectorFieldOperations.GetOrderedNeighbors(dag, node.Id);

        // Assert
        neighbors.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDivergence_ThrowsArgumentNullException_WhenDagIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VectorFieldOperations.ComputeDivergence(
                null!,
                Guid.NewGuid(),
                _ => new float[3]));
    }

    [Fact]
    public void ComputeDivergence_ThrowsArgumentNullException_WhenGetEmbeddingIsNull()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VectorFieldOperations.ComputeDivergence(
                dag,
                Guid.NewGuid(),
                null!));
    }
}

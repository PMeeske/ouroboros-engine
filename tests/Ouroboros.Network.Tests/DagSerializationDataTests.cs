// <copyright file="DagSerializationDataTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class DagSerializationDataTests
{
    [Fact]
    public void Ctor_SetsNodesAndEdges()
    {
        // Arrange
        var nodes = new[]
        {
            new NodeData(Guid.NewGuid(), "TypeA", "{}", DateTimeOffset.UtcNow, Array.Empty<Guid>(), "h1"),
            new NodeData(Guid.NewGuid(), "TypeB", "{}", DateTimeOffset.UtcNow, Array.Empty<Guid>(), "h2"),
        };

        var edges = new[]
        {
            new EdgeData(
                Guid.NewGuid(),
                new[] { nodes[0].Id },
                nodes[1].Id,
                "Transform",
                "{}",
                DateTimeOffset.UtcNow,
                0.9,
                100,
                "eh1"),
        };

        // Act
        var data = new DagSerializationData(nodes, edges);

        // Assert
        data.Nodes.Should().HaveCount(2);
        data.Edges.Should().HaveCount(1);
    }

    [Fact]
    public void Ctor_EmptyArrays_Succeeds()
    {
        // Act
        var data = new DagSerializationData(Array.Empty<NodeData>(), Array.Empty<EdgeData>());

        // Assert
        data.Nodes.Should().BeEmpty();
        data.Edges.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_SameArrays_AreEqual()
    {
        // Arrange
        var nodes = Array.Empty<NodeData>();
        var edges = Array.Empty<EdgeData>();

        // Act
        var a = new DagSerializationData(nodes, edges);
        var b = new DagSerializationData(nodes, edges);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Nodes_ReturnsPassedArray()
    {
        // Arrange
        var node = new NodeData(
            Guid.NewGuid(), "Test", "{\"x\":1}", DateTimeOffset.UtcNow, Array.Empty<Guid>(), "hash");
        var nodes = new[] { node };

        // Act
        var data = new DagSerializationData(nodes, Array.Empty<EdgeData>());

        // Assert
        data.Nodes.Should().ContainSingle();
        data.Nodes[0].TypeName.Should().Be("Test");
    }

    [Fact]
    public void Edges_ReturnsPassedArray()
    {
        // Arrange
        var edge = new EdgeData(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            Guid.NewGuid(),
            "Op",
            "{}",
            DateTimeOffset.UtcNow,
            null,
            null,
            "hash");
        var edges = new[] { edge };

        // Act
        var data = new DagSerializationData(Array.Empty<NodeData>(), edges);

        // Assert
        data.Edges.Should().ContainSingle();
        data.Edges[0].OperationName.Should().Be("Op");
    }
}

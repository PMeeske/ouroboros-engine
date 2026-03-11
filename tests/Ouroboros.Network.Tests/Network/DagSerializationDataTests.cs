// <copyright file="DagSerializationDataTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class DagSerializationDataTests
{
    [Fact]
    public void Constructor_SetsNodesAndEdges()
    {
        // Arrange
        var nodes = new[] { CreateNode() };
        var edges = new[] { CreateEdge() };

        // Act
        var sut = new DagSerializationData(nodes, edges);

        // Assert
        sut.Nodes.Should().BeSameAs(nodes);
        sut.Edges.Should().BeSameAs(edges);
    }

    [Fact]
    public void Constructor_EmptyArrays_IsAccepted()
    {
        // Arrange & Act
        var sut = new DagSerializationData(
            Array.Empty<NodeData>(),
            Array.Empty<EdgeData>());

        // Assert
        sut.Nodes.Should().BeEmpty();
        sut.Edges.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithPopulatedData_ContainsAllElements()
    {
        // Arrange
        var nodes = new[] { CreateNode(), CreateNode() };
        var edges = new[] { CreateEdge(), CreateEdge(), CreateEdge() };

        // Act
        var sut = new DagSerializationData(nodes, edges);

        // Assert
        sut.Nodes.Should().HaveCount(2);
        sut.Edges.Should().HaveCount(3);
    }

    [Fact]
    public void RecordEquality_IdenticalReferences_AreEqual()
    {
        // Arrange
        var nodes = new[] { CreateNode() };
        var edges = new[] { CreateEdge() };

        // Act
        var a = new DagSerializationData(nodes, edges);
        var b = new DagSerializationData(nodes, edges);

        // Assert
        a.Should().Be(b);
    }

    private static NodeData CreateNode() =>
        new(
            Guid.NewGuid(),
            "TestType",
            "{\"data\":1}",
            DateTimeOffset.UtcNow,
            Array.Empty<Guid>(),
            "nodehash");

    private static EdgeData CreateEdge() =>
        new(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            Guid.NewGuid(),
            "Op",
            "{}",
            DateTimeOffset.UtcNow,
            0.9,
            100L,
            "edgehash");
}

// <copyright file="NodeDataTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class NodeDataTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var typeName = "Draft";
        var payloadJson = "{\"text\":\"hello\"}";
        var createdAt = DateTimeOffset.UtcNow;
        var parentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var hash = "abc123def456";

        // Act
        var nodeData = new NodeData(id, typeName, payloadJson, createdAt, parentIds, hash);

        // Assert
        nodeData.Id.Should().Be(id);
        nodeData.TypeName.Should().Be(typeName);
        nodeData.PayloadJson.Should().Be(payloadJson);
        nodeData.CreatedAt.Should().Be(createdAt);
        nodeData.ParentIds.Should().BeEquivalentTo(parentIds);
        nodeData.Hash.Should().Be(hash);
    }

    [Fact]
    public void Ctor_EmptyParentIds_Succeeds()
    {
        // Act
        var nodeData = new NodeData(
            Guid.NewGuid(), "Root", "{}", DateTimeOffset.UtcNow, Array.Empty<Guid>(), "hash");

        // Assert
        nodeData.ParentIds.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var parentIds = new[] { Guid.NewGuid() };

        // Act
        var a = new NodeData(id, "T", "{}", now, parentIds, "hash");
        var b = new NodeData(id, "T", "{}", now, parentIds, "hash");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var a = new NodeData(Guid.NewGuid(), "TypeA", "{}", now, Array.Empty<Guid>(), "hash1");
        var b = new NodeData(Guid.NewGuid(), "TypeB", "{}", now, Array.Empty<Guid>(), "hash2");

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void ToString_ContainsTypeName()
    {
        // Arrange
        var nodeData = new NodeData(
            Guid.NewGuid(), "TestType", "{}", DateTimeOffset.UtcNow, Array.Empty<Guid>(), "hash");

        // Act
        var str = nodeData.ToString();

        // Assert
        str.Should().Contain("TestType");
    }
}

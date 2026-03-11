// <copyright file="NodeDataTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class NodeDataTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var typeName = "TestType";
        var payloadJson = "{\"data\":42}";
        var createdAt = DateTimeOffset.UtcNow;
        var parentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var hash = "sha256hash";

        // Act
        var sut = new NodeData(id, typeName, payloadJson, createdAt, parentIds, hash);

        // Assert
        sut.Id.Should().Be(id);
        sut.TypeName.Should().Be(typeName);
        sut.PayloadJson.Should().Be(payloadJson);
        sut.CreatedAt.Should().Be(createdAt);
        sut.ParentIds.Should().BeEquivalentTo(parentIds);
        sut.Hash.Should().Be(hash);
    }

    [Fact]
    public void Constructor_EmptyParentIds_IsAccepted()
    {
        // Arrange & Act
        var sut = new NodeData(
            Guid.NewGuid(),
            "RootNode",
            "{}",
            DateTimeOffset.UtcNow,
            Array.Empty<Guid>(),
            "hash");

        // Assert
        sut.ParentIds.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_IdenticalValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var parentIds = new[] { Guid.NewGuid() };
        var createdAt = DateTimeOffset.UtcNow;

        // Act
        var a = new NodeData(id, "Type", "{}", createdAt, parentIds, "hash");
        var b = new NodeData(id, "Type", "{}", createdAt, parentIds, "hash");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new NodeData(
            Guid.NewGuid(),
            "OriginalType",
            "{\"key\":\"value\"}",
            DateTimeOffset.UtcNow,
            new[] { Guid.NewGuid() },
            "hash");

        // Act
        var modified = original with { TypeName = "ModifiedType" };

        // Assert
        modified.TypeName.Should().Be("ModifiedType");
        modified.Id.Should().Be(original.Id);
        modified.PayloadJson.Should().Be(original.PayloadJson);
        modified.Should().NotBe(original);
    }
}

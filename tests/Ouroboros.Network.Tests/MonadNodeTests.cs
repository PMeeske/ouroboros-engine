// <copyright file="MonadNodeTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using FluentAssertions;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class MonadNodeTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var parentIds = ImmutableArray.Create(Guid.NewGuid());

        // Act
        var node = new MonadNode(id, "Draft", "{\"text\":\"hello\"}", now, parentIds);

        // Assert
        node.Id.Should().Be(id);
        node.TypeName.Should().Be("Draft");
        node.PayloadJson.Should().Be("{\"text\":\"hello\"}");
        node.CreatedAt.Should().Be(now);
        node.ParentIds.Should().HaveCount(1);
    }

    [Fact]
    public void Ctor_NullTypeName_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new MonadNode(
            Guid.NewGuid(), null!, "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("typeName");
    }

    [Fact]
    public void Ctor_NullPayloadJson_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new MonadNode(
            Guid.NewGuid(), "Test", null!, DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("payloadJson");
    }

    [Fact]
    public void Ctor_HashIsComputedOnConstruction()
    {
        // Arrange & Act
        var node = new MonadNode(
            Guid.NewGuid(), "Test", "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);

        // Assert
        node.Hash.Should().NotBeNullOrEmpty();
        node.Hash.Should().HaveLength(64, "SHA256 produces a 64 hex-character string");
    }

    [Fact]
    public void VerifyHash_ReturnsTrueForValidNode()
    {
        // Arrange
        var node = new MonadNode(
            Guid.NewGuid(), "Test", "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);

        // Act
        var result = node.VerifyHash();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void FromPayload_CreatesNodeWithSerializedJson()
    {
        // Arrange
        var payload = new { Value = 42, Name = "test" };

        // Act
        var node = MonadNode.FromPayload("TestPayload", payload);

        // Assert
        node.TypeName.Should().Be("TestPayload");
        node.PayloadJson.Should().Contain("42");
        node.PayloadJson.Should().Contain("test");
        node.VerifyHash().Should().BeTrue();
    }

    [Fact]
    public void FromPayload_WithDefaultParentIds_UsesEmpty()
    {
        // Arrange & Act
        var node = MonadNode.FromPayload("T", "data");

        // Assert
        node.ParentIds.Should().BeEmpty();
    }

    [Fact]
    public void FromPayload_WithParentIds_SetsParents()
    {
        // Arrange
        var parentId = Guid.NewGuid();

        // Act
        var node = MonadNode.FromPayload("T", "data", ImmutableArray.Create(parentId));

        // Assert
        node.ParentIds.Should().ContainSingle().Which.Should().Be(parentId);
    }

    [Fact]
    public void DeserializePayload_ValidJson_ReturnsSome()
    {
        // Arrange
        var node = MonadNode.FromPayload("Test", new { Name = "test" });

        // Act
        var result = node.DeserializePayload<Dictionary<string, object>>();

        // Assert
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void DeserializePayload_InvalidJson_ReturnsNone()
    {
        // Arrange
        var node = new MonadNode(
            Guid.NewGuid(), "T", "not-valid-json", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);

        // Act
        var result = node.DeserializePayload<int>();

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var parentIds = ImmutableArray<Guid>.Empty;

        // Act
        var a = new MonadNode(id, "T", "{}", now, parentIds);
        var b = new MonadNode(id, "T", "{}", now, parentIds);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Hash_IsDeterministic_SameInputsProduceSameHash()
    {
        // Arrange
        var id = Guid.NewGuid();
        var time = DateTimeOffset.UtcNow;
        var parentIds = ImmutableArray.Create(Guid.NewGuid());

        // Act
        var a = new MonadNode(id, "TypeA", "{\"key\":\"val\"}", time, parentIds);
        var b = new MonadNode(id, "TypeA", "{\"key\":\"val\"}", time, parentIds);

        // Assert
        a.Hash.Should().Be(b.Hash);
    }

    [Fact]
    public void Hash_DifferentInputs_ProduceDifferentHash()
    {
        // Arrange
        var id = Guid.NewGuid();
        var time = DateTimeOffset.UtcNow;
        var parentIds = ImmutableArray<Guid>.Empty;

        // Act
        var a = new MonadNode(id, "TypeA", "{}", time, parentIds);
        var b = new MonadNode(id, "TypeB", "{}", time, parentIds);

        // Assert
        a.Hash.Should().NotBe(b.Hash);
    }
}

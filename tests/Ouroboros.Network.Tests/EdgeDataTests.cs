// <copyright file="EdgeDataTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class EdgeDataTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var inputIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var outputId = Guid.NewGuid();
        var operationName = "Transform";
        var operationSpecJson = "{\"config\":true}";
        var createdAt = DateTimeOffset.UtcNow;
        double? confidence = 0.95;
        long? durationMs = 150;
        var hash = "edge_hash_123";

        // Act
        var edgeData = new EdgeData(
            id, inputIds, outputId, operationName, operationSpecJson,
            createdAt, confidence, durationMs, hash);

        // Assert
        edgeData.Id.Should().Be(id);
        edgeData.InputIds.Should().BeEquivalentTo(inputIds);
        edgeData.OutputId.Should().Be(outputId);
        edgeData.OperationName.Should().Be(operationName);
        edgeData.OperationSpecJson.Should().Be(operationSpecJson);
        edgeData.CreatedAt.Should().Be(createdAt);
        edgeData.Confidence.Should().Be(confidence);
        edgeData.DurationMs.Should().Be(durationMs);
        edgeData.Hash.Should().Be(hash);
    }

    [Fact]
    public void Ctor_NullConfidence_Succeeds()
    {
        // Act
        var edgeData = new EdgeData(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            Guid.NewGuid(),
            "Op",
            "{}",
            DateTimeOffset.UtcNow,
            null,
            null,
            "hash");

        // Assert
        edgeData.Confidence.Should().BeNull();
        edgeData.DurationMs.Should().BeNull();
    }

    [Fact]
    public void Ctor_SingleInputId_Succeeds()
    {
        // Act
        var edgeData = new EdgeData(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            Guid.NewGuid(),
            "Single",
            "{}",
            DateTimeOffset.UtcNow,
            0.5,
            100,
            "hash");

        // Assert
        edgeData.InputIds.Should().HaveCount(1);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var inputIds = new[] { Guid.NewGuid() };
        var outputId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Act
        var a = new EdgeData(id, inputIds, outputId, "Op", "{}", now, 0.9, 200, "h");
        var b = new EdgeData(id, inputIds, outputId, "Op", "{}", now, 0.9, 200, "h");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var a = new EdgeData(Guid.NewGuid(), new[] { Guid.NewGuid() }, Guid.NewGuid(), "OpA", "{}", now, null, null, "h1");
        var b = new EdgeData(Guid.NewGuid(), new[] { Guid.NewGuid() }, Guid.NewGuid(), "OpB", "{}", now, null, null, "h2");

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void ToString_ContainsOperationName()
    {
        // Arrange
        var edgeData = new EdgeData(
            Guid.NewGuid(), new[] { Guid.NewGuid() }, Guid.NewGuid(),
            "TestOperation", "{}", DateTimeOffset.UtcNow, null, null, "hash");

        // Act
        var str = edgeData.ToString();

        // Assert
        str.Should().Contain("TestOperation");
    }
}

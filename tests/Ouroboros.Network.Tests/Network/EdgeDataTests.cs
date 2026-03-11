// <copyright file="EdgeDataTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class EdgeDataTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var inputIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var outputId = Guid.NewGuid();
        var operationName = "Transform";
        var operationSpecJson = "{\"key\":\"value\"}";
        var createdAt = DateTimeOffset.UtcNow;
        var confidence = 0.95;
        var durationMs = 150L;
        var hash = "abc123";

        // Act
        var sut = new EdgeData(id, inputIds, outputId, operationName, operationSpecJson, createdAt, confidence, durationMs, hash);

        // Assert
        sut.Id.Should().Be(id);
        sut.InputIds.Should().BeEquivalentTo(inputIds);
        sut.OutputId.Should().Be(outputId);
        sut.OperationName.Should().Be(operationName);
        sut.OperationSpecJson.Should().Be(operationSpecJson);
        sut.CreatedAt.Should().Be(createdAt);
        sut.Confidence.Should().Be(confidence);
        sut.DurationMs.Should().Be(durationMs);
        sut.Hash.Should().Be(hash);
    }

    [Fact]
    public void Confidence_Null_IsAccepted()
    {
        // Arrange & Act
        var sut = new EdgeData(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            Guid.NewGuid(),
            "Op",
            "{}",
            DateTimeOffset.UtcNow,
            null,
            100L,
            "hash");

        // Assert
        sut.Confidence.Should().BeNull();
    }

    [Fact]
    public void Confidence_WithValue_IsSet()
    {
        // Arrange & Act
        var sut = new EdgeData(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            Guid.NewGuid(),
            "Op",
            "{}",
            DateTimeOffset.UtcNow,
            0.75,
            100L,
            "hash");

        // Assert
        sut.Confidence.Should().Be(0.75);
    }

    [Fact]
    public void DurationMs_Null_IsAccepted()
    {
        // Arrange & Act
        var sut = new EdgeData(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            Guid.NewGuid(),
            "Op",
            "{}",
            DateTimeOffset.UtcNow,
            0.5,
            null,
            "hash");

        // Assert
        sut.DurationMs.Should().BeNull();
    }

    [Fact]
    public void DurationMs_WithValue_IsSet()
    {
        // Arrange & Act
        var sut = new EdgeData(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            Guid.NewGuid(),
            "Op",
            "{}",
            DateTimeOffset.UtcNow,
            0.5,
            250L,
            "hash");

        // Assert
        sut.DurationMs.Should().Be(250L);
    }

    [Fact]
    public void RecordEquality_IdenticalValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var inputIds = new[] { Guid.NewGuid() };
        var outputId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        // Act
        var a = new EdgeData(id, inputIds, outputId, "Op", "{}", createdAt, 0.9, 100L, "hash");
        var b = new EdgeData(id, inputIds, outputId, "Op", "{}", createdAt, 0.9, 100L, "hash");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new EdgeData(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            Guid.NewGuid(),
            "Op",
            "{}",
            DateTimeOffset.UtcNow,
            0.5,
            100L,
            "hash");

        // Act
        var modified = original with { OperationName = "NewOp" };

        // Assert
        modified.OperationName.Should().Be("NewOp");
        modified.Id.Should().Be(original.Id);
        modified.OutputId.Should().Be(original.OutputId);
        modified.Should().NotBe(original);
    }
}

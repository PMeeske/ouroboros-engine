// <copyright file="TransitionEdgeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class TransitionEdgeTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Act
        var edge = new TransitionEdge(
            id,
            ImmutableArray.Create(inputId),
            outputId,
            "Critique",
            "{\"prompt\":\"review\"}",
            now,
            confidence: 0.95,
            durationMs: 150);

        // Assert
        edge.Id.Should().Be(id);
        edge.InputIds.Should().ContainSingle().Which.Should().Be(inputId);
        edge.OutputId.Should().Be(outputId);
        edge.OperationName.Should().Be("Critique");
        edge.OperationSpecJson.Should().Be("{\"prompt\":\"review\"}");
        edge.CreatedAt.Should().Be(now);
        edge.Confidence.Should().Be(0.95);
        edge.DurationMs.Should().Be(150);
    }

    [Fact]
    public void Ctor_EmptyInputIds_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray<Guid>.Empty,
            Guid.NewGuid(),
            "op",
            "{}",
            DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("inputIds");
    }

    [Fact]
    public void Ctor_DefaultInputIds_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new TransitionEdge(
            Guid.NewGuid(),
            default,
            Guid.NewGuid(),
            "op",
            "{}",
            DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("inputIds");
    }

    [Fact]
    public void Ctor_NullOperationName_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            null!,
            "{}",
            DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("operationName");
    }

    [Fact]
    public void Ctor_NullOperationSpecJson_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            "op",
            null!,
            DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("operationSpecJson");
    }

    [Fact]
    public void Ctor_HashIsComputedOnConstruction()
    {
        // Arrange & Act
        var edge = new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            "op",
            "{}",
            DateTimeOffset.UtcNow);

        // Assert
        edge.Hash.Should().NotBeNullOrEmpty();
        edge.Hash.Should().HaveLength(64, "SHA256 produces a 64 hex-character string");
    }

    [Fact]
    public void VerifyHash_ReturnsTrueForValidEdge()
    {
        // Arrange
        var edge = TransitionEdge.CreateSimple(
            Guid.NewGuid(), Guid.NewGuid(), "Op", new { Param = "value" });

        // Act
        var result = edge.VerifyHash();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Create_CreatesEdgeWithSerializedSpec()
    {
        // Arrange
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();

        // Act
        var edge = TransitionEdge.Create(
            ImmutableArray.Create(inputId),
            outputId,
            "Transform",
            new { Config = true, Level = 3 },
            0.8,
            200);

        // Assert
        edge.InputIds.Should().ContainSingle().Which.Should().Be(inputId);
        edge.OutputId.Should().Be(outputId);
        edge.OperationName.Should().Be("Transform");
        edge.OperationSpecJson.Should().Contain("Config");
        edge.OperationSpecJson.Should().Contain("true");
        edge.Confidence.Should().Be(0.8);
        edge.DurationMs.Should().Be(200);
        edge.VerifyHash().Should().BeTrue();
    }

    [Fact]
    public void CreateSimple_WrapsSingleInputInArray()
    {
        // Arrange
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();

        // Act
        var edge = TransitionEdge.CreateSimple(inputId, outputId, "Op", "spec");

        // Assert
        edge.InputIds.Should().ContainSingle().Which.Should().Be(inputId);
        edge.OutputId.Should().Be(outputId);
    }

    [Fact]
    public void DeserializeOperationSpec_ValidJson_ReturnsSome()
    {
        // Arrange
        var edge = TransitionEdge.CreateSimple(
            Guid.NewGuid(), Guid.NewGuid(), "Op", new { Key = "value" });

        // Act
        var result = edge.DeserializeOperationSpec<Dictionary<string, object>>();

        // Assert
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void DeserializeOperationSpec_InvalidJson_ReturnsNone()
    {
        // Arrange
        var edge = new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            "Op",
            "not-valid-json",
            DateTimeOffset.UtcNow);

        // Act
        var result = edge.DeserializeOperationSpec<int>();

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Ctor_OptionalConfidenceAndDurationMs_DefaultToNull()
    {
        // Arrange & Act
        var edge = new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            "Op",
            "{}",
            DateTimeOffset.UtcNow);

        // Assert
        edge.Confidence.Should().BeNull();
        edge.DurationMs.Should().BeNull();
    }

    [Fact]
    public void Hash_IsDeterministic_SameInputsProduceSameHash()
    {
        // Arrange
        var id = Guid.NewGuid();
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Act
        var a = new TransitionEdge(
            id, ImmutableArray.Create(inputId), outputId, "Op", "{}", now, 0.9, 100);
        var b = new TransitionEdge(
            id, ImmutableArray.Create(inputId), outputId, "Op", "{}", now, 0.9, 100);

        // Assert
        a.Hash.Should().Be(b.Hash);
    }

    [Fact]
    public void Hash_DifferentInputs_ProduceDifferentHash()
    {
        // Arrange
        var id = Guid.NewGuid();
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Act
        var a = new TransitionEdge(
            id, ImmutableArray.Create(inputId), outputId, "OpA", "{}", now);
        var b = new TransitionEdge(
            id, ImmutableArray.Create(inputId), outputId, "OpB", "{}", now);

        // Assert
        a.Hash.Should().NotBe(b.Hash);
    }
}

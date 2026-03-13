// <copyright file="GlobalNetworkStateTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using FluentAssertions;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class GlobalNetworkStateTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var nodeId = Guid.NewGuid();
        var leafId = Guid.NewGuid();

        // Act
        var state = new GlobalNetworkState(
            epoch: 5,
            timestamp: now,
            totalNodes: 10,
            totalTransitions: 8,
            nodeCountByType: ImmutableDictionary<string, int>.Empty.Add("Draft", 5),
            transitionCountByOperation: ImmutableDictionary<string, int>.Empty.Add("Critique", 3),
            rootNodeIds: ImmutableArray.Create(nodeId),
            leafNodeIds: ImmutableArray.Create(leafId),
            averageConfidence: 0.85,
            totalProcessingTimeMs: 5000);

        // Assert
        state.Epoch.Should().Be(5);
        state.Timestamp.Should().Be(now);
        state.TotalNodes.Should().Be(10);
        state.TotalTransitions.Should().Be(8);
        state.NodeCountByType.Should().ContainKey("Draft").WhoseValue.Should().Be(5);
        state.TransitionCountByOperation.Should().ContainKey("Critique").WhoseValue.Should().Be(3);
        state.RootNodeIds.Should().ContainSingle().Which.Should().Be(nodeId);
        state.LeafNodeIds.Should().ContainSingle().Which.Should().Be(leafId);
        state.AverageConfidence.Should().Be(0.85);
        state.TotalProcessingTimeMs.Should().Be(5000);
        state.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_NullNodeCountByType_DefaultsToEmpty()
    {
        // Arrange & Act
        var state = new GlobalNetworkState(
            0, DateTimeOffset.UtcNow, 0, 0, null!, null!,
            ImmutableArray<Guid>.Empty, ImmutableArray<Guid>.Empty);

        // Assert
        state.NodeCountByType.Should().BeEmpty();
        state.TransitionCountByOperation.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_NullMetadata_DefaultsToEmpty()
    {
        // Arrange & Act
        var state = new GlobalNetworkState(
            0, DateTimeOffset.UtcNow, 0, 0,
            ImmutableDictionary<string, int>.Empty,
            ImmutableDictionary<string, int>.Empty,
            ImmutableArray<Guid>.Empty,
            ImmutableArray<Guid>.Empty,
            metadata: null);

        // Assert
        state.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_WithMetadata_SetsMetadata()
    {
        // Arrange
        var metadata = ImmutableDictionary<string, string>.Empty.Add("key", "value");

        // Act
        var state = new GlobalNetworkState(
            1, DateTimeOffset.UtcNow, 0, 0,
            ImmutableDictionary<string, int>.Empty,
            ImmutableDictionary<string, int>.Empty,
            ImmutableArray<Guid>.Empty,
            ImmutableArray<Guid>.Empty,
            metadata: metadata);

        // Assert
        state.Metadata.Should().ContainKey("key").WhoseValue.Should().Be("value");
    }

    [Fact]
    public void Ctor_OptionalParametersOmitted_DefaultsToNull()
    {
        // Arrange & Act
        var state = new GlobalNetworkState(
            1, DateTimeOffset.UtcNow, 5, 3,
            ImmutableDictionary<string, int>.Empty,
            ImmutableDictionary<string, int>.Empty,
            ImmutableArray<Guid>.Empty,
            ImmutableArray<Guid>.Empty);

        // Assert
        state.AverageConfidence.Should().BeNull();
        state.TotalProcessingTimeMs.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var rootId = Guid.NewGuid();
        var leafId = Guid.NewGuid();
        var nodeCountByType = ImmutableDictionary<string, int>.Empty.Add("Draft", 2);
        var transitionCountByOp = ImmutableDictionary<string, int>.Empty.Add("Critique", 1);
        var rootIds = ImmutableArray.Create(rootId);
        var leafIds = ImmutableArray.Create(leafId);

        // Act
        var a = new GlobalNetworkState(
            1, now, 5, 3, nodeCountByType, transitionCountByOp,
            rootIds, leafIds, 0.9, 1000);
        var b = new GlobalNetworkState(
            1, now, 5, 3, nodeCountByType, transitionCountByOp,
            rootIds, leafIds, 0.9, 1000);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var emptyDict = ImmutableDictionary<string, int>.Empty;

        var a = new GlobalNetworkState(
            1, now, 5, 3, emptyDict, emptyDict,
            ImmutableArray<Guid>.Empty, ImmutableArray<Guid>.Empty);
        var b = new GlobalNetworkState(
            2, now, 5, 3, emptyDict, emptyDict,
            ImmutableArray<Guid>.Empty, ImmutableArray<Guid>.Empty);

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new GlobalNetworkState(
            1, DateTimeOffset.UtcNow, 5, 3,
            ImmutableDictionary<string, int>.Empty,
            ImmutableDictionary<string, int>.Empty,
            ImmutableArray<Guid>.Empty,
            ImmutableArray<Guid>.Empty,
            averageConfidence: 0.5);

        // Act
        var modified = original with { Epoch = 2, TotalNodes = 10 };

        // Assert
        modified.Epoch.Should().Be(2);
        modified.TotalNodes.Should().Be(10);
        modified.TotalTransitions.Should().Be(original.TotalTransitions);
        modified.AverageConfidence.Should().Be(original.AverageConfidence);
        modified.Timestamp.Should().Be(original.Timestamp);
    }
}

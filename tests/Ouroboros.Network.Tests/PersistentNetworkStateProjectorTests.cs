// <copyright file="PersistentNetworkStateProjectorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using FluentAssertions;
using Moq;
using Ouroboros.Core.Configuration;
using Qdrant.Client;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class PersistentNetworkStateProjectorTests : IAsyncDisposable
{
    private readonly MerkleDag _dag;
    private readonly QdrantClient _client;
    private readonly Mock<IQdrantCollectionRegistry> _registry;
    private readonly Func<string, CancellationToken, Task<float[]>> _embeddingFunc;

    public PersistentNetworkStateProjectorTests()
    {
        _dag = new MerkleDag();
        _client = new QdrantClient("localhost", 6334);
        _registry = new Mock<IQdrantCollectionRegistry>();
        _registry.Setup(r => r.GetCollectionName(QdrantCollectionRole.NetworkSnapshots))
            .Returns("test_snapshots");
        _registry.Setup(r => r.GetCollectionName(QdrantCollectionRole.NetworkLearnings))
            .Returns("test_learnings");
        _embeddingFunc = (_, _) => Task.FromResult(new float[384]);
    }

    #region Constructor Tests

    [Fact]
    public void Ctor_NullDag_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PersistentNetworkStateProjector(
            null!, _client, _registry.Object, _embeddingFunc);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("dag");
    }

    [Fact]
    public void Ctor_NullClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PersistentNetworkStateProjector(
            _dag, null!, _registry.Object, _embeddingFunc);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("client");
    }

    [Fact]
    public void Ctor_NullRegistry_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PersistentNetworkStateProjector(
            _dag, _client, null!, _embeddingFunc);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("registry");
    }

    [Fact]
    public void Ctor_NullEmbeddingFunc_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PersistentNetworkStateProjector(
            _dag, _client, _registry.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("embeddingFunc");
    }

    [Fact]
    public void Ctor_ValidArgs_InitializesCorrectly()
    {
        // Act
        var projector = new PersistentNetworkStateProjector(
            _dag, _client, _registry.Object, _embeddingFunc);

        // Assert
        projector.CurrentEpoch.Should().Be(0);
        projector.Snapshots.Should().BeEmpty();
        projector.RecentLearnings.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_NullLogger_DoesNotThrow()
    {
        // Act
        var act = () => new PersistentNetworkStateProjector(
            _dag, _client, _registry.Object, _embeddingFunc, logger: null);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Properties

    [Fact]
    public void CurrentEpoch_InitialValue_IsZero()
    {
        // Arrange
        var projector = CreateProjector();

        // Assert
        projector.CurrentEpoch.Should().Be(0);
    }

    [Fact]
    public void Snapshots_InitialValue_IsEmpty()
    {
        // Arrange
        var projector = CreateProjector();

        // Assert
        projector.Snapshots.Should().BeEmpty();
    }

    [Fact]
    public void RecentLearnings_InitialValue_IsEmpty()
    {
        // Arrange
        var projector = CreateProjector();

        // Assert
        projector.RecentLearnings.Should().BeEmpty();
    }

    #endregion

    #region DisposeAsync

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        // Arrange
        var projector = CreateProjector();

        // Act & Assert
        await FluentActions.Invoking(async () => await projector.DisposeAsync())
            .Should().NotThrowAsync();
    }

    #endregion

    #region Helper Methods

    private PersistentNetworkStateProjector CreateProjector()
    {
        return new PersistentNetworkStateProjector(
            _dag, _client, _registry.Object, _embeddingFunc);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await Task.CompletedTask;
    }

    #endregion
}

namespace Ouroboros.Network.Tests;

using System.Collections.Immutable;

[Trait("Category", "Unit")]
public sealed class QdrantDagStoreTests
{
    #region Construction (Obsolete constructor)

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new QdrantDagStore(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("config");
    }

    [Fact]
    public void Constructor_DefaultConfig_CreatesInstance()
    {
        // Arrange
        var config = new QdrantDagConfig();

        // Act
        var store = new QdrantDagStore(config);

        // Assert
        store.Should().NotBeNull();
        store.SupportsSemanticSearch.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithEmbeddingFunc_SupportsSemanticSearch()
    {
        // Arrange
        var config = new QdrantDagConfig();

        // Act
        var store = new QdrantDagStore(config, _ => Task.FromResult(new float[10]));

        // Assert
        store.SupportsSemanticSearch.Should().BeTrue();
    }

    #endregion

    #region SaveNodeAsync / SaveEdgeAsync / SaveDagAsync

    [Fact]
    public async Task SaveNodeAsync_NullNode_ReturnsFailure()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);

        try
        {
            // Act
            var result = await store.SaveNodeAsync(null!);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be("Node cannot be null");
        }
        finally
        {
            await store.DisposeAsync();
        }
    }

    [Fact]
    public async Task SaveEdgeAsync_NullEdge_ReturnsFailure()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);

        try
        {
            // Act
            var result = await store.SaveEdgeAsync(null!);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be("Edge cannot be null");
        }
        finally
        {
            await store.DisposeAsync();
        }
    }

    [Fact]
    public async Task SaveDagAsync_NullDag_ReturnsFailure()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);

        try
        {
            // Act
            var result = await store.SaveDagAsync(null!);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be("DAG cannot be null");
        }
        finally
        {
            await store.DisposeAsync();
        }
    }

    #endregion

    #region SearchNodesAsync

    [Fact]
    public async Task SearchNodesAsync_WithoutEmbedding_ReturnsFailure()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);

        try
        {
            // Act
            var result = await store.SearchNodesAsync("query");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Contain("embedding function");
        }
        finally
        {
            await store.DisposeAsync();
        }
    }

    #endregion

    #region DisposeAsync

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);

        // Act
        Func<Task> act = async () => await store.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);
        await store.DisposeAsync();

        // Act
        Func<Task> act = async () => await store.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}

[Trait("Category", "Unit")]
public sealed class PersistentNetworkStateProjectorTests
{
    #region Construction (Obsolete constructor)

    [Fact]
    public void Constructor_NullDag_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new PersistentNetworkStateProjector(null!, "http://localhost:6334", _ => Task.FromResult(new float[10]));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("dag");
    }

    [Fact]
    public void Constructor_NullEndpoint_UsesDefault()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act
        var projector = new PersistentNetworkStateProjector(dag, null!, _ => Task.FromResult(new float[10]));

        // Assert
        projector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullEmbeddingFunc_ThrowsArgumentNullException()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act
        Action act = () => new PersistentNetworkStateProjector(dag, "http://localhost:6334", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("embeddingFunc");
    }

    #endregion

    #region Properties

    [Fact]
    public void CurrentEpoch_InitialValue_IsZero()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new PersistentNetworkStateProjector(dag, "http://localhost:6334", _ => Task.FromResult(new float[10]));

        // Assert
        projector.CurrentEpoch.Should().Be(0);
        projector.Snapshots.Should().BeEmpty();
        projector.RecentLearnings.Should().BeEmpty();
    }

    #endregion

    #region RecordLearningAsync

    [Fact]
    public async Task RecordLearningAsync_CreatesLearning()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new PersistentNetworkStateProjector(dag, "http://localhost:6334", _ => Task.FromResult(new float[10]));

        try
        {
            // Act
            await projector.RecordLearningAsync("skill", "content", "context", 0.95);

            // Assert
            projector.RecentLearnings.Should().ContainSingle();
            projector.RecentLearnings[0].Category.Should().Be("skill");
            projector.RecentLearnings[0].Content.Should().Be("content");
            projector.RecentLearnings[0].Confidence.Should().Be(0.95);
        }
        finally
        {
            await projector.DisposeAsync();
        }
    }

    #endregion

    #region GetRelevantLearningsAsync / GetLearningsByCategoryAsync

    [Fact]
    public async Task GetRelevantLearningsAsync_ReturnsEmptyWhenNoData()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new PersistentNetworkStateProjector(dag, "http://localhost:6334", _ => Task.FromResult(new float[10]));

        try
        {
            // Act
            var result = await projector.GetRelevantLearningsAsync("context");

            // Assert
            result.Should().BeEmpty();
        }
        finally
        {
            await projector.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetLearningsByCategoryAsync_ReturnsEmptyWhenNoData()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new PersistentNetworkStateProjector(dag, "http://localhost:6334", _ => Task.FromResult(new float[10]));

        try
        {
            // Act
            var result = await projector.GetLearningsByCategoryAsync("skill");

            // Assert
            result.Should().BeEmpty();
        }
        finally
        {
            await projector.DisposeAsync();
        }
    }

    #endregion

    #region DisposeAsync

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new PersistentNetworkStateProjector(dag, "http://localhost:6334", _ => Task.FromResult(new float[10]));

        // Act
        Func<Task> act = async () => await projector.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}

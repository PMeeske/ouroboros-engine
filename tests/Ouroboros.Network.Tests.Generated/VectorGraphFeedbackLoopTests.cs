namespace Ouroboros.Network.Tests;

using System.Collections.Immutable;

[Trait("Category", "Unit")]
public sealed class VectorGraphFeedbackLoopTests
{
    #region Construction

    [Fact]
    public void Constructor_NullStore_ThrowsArgumentNullException()
    {
        // Arrange
        var mettaEngine = new Mock<IMeTTaEngine>().Object;
        var embeddingModel = new Mock<IEmbeddingModel>().Object;

        // Act
        Action act = () => new VectorGraphFeedbackLoop(null!, mettaEngine, embeddingModel);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("store");
    }

    [Fact]
    public void Constructor_NullMeTTaEngine_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);
        var embeddingModel = new Mock<IEmbeddingModel>().Object;

        try
        {
            // Act
            Action act = () => new VectorGraphFeedbackLoop(store, null!, embeddingModel);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("mettaEngine");
        }
        finally
        {
            store.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    [Fact]
    public void Constructor_NullEmbeddingModel_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);
        var mettaEngine = new Mock<IMeTTaEngine>().Object;

        try
        {
            // Act
            Action act = () => new VectorGraphFeedbackLoop(store, mettaEngine, null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("embeddingModel");
        }
        finally
        {
            store.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    [Fact]
    public void Constructor_WithDefaults_UsesDefaultConfig()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);
        var mettaEngine = new Mock<IMeTTaEngine>().Object;
        var embeddingModel = new Mock<IEmbeddingModel>().Object;

        try
        {
            // Act
            var loop = new VectorGraphFeedbackLoop(store, mettaEngine, embeddingModel);

            // Assert
            loop.Should().NotBeNull();
        }
        finally
        {
            store.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    [Fact]
    public void Constructor_WithCustomConfig_UsesConfig()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);
        var mettaEngine = new Mock<IMeTTaEngine>().Object;
        var embeddingModel = new Mock<IEmbeddingModel>().Object;
        var loopConfig = new FeedbackLoopConfig(DivergenceThreshold: 0.8f, RotationThreshold: 0.5f, MaxModificationsPerCycle: 5, AutoPersist: false);

        try
        {
            // Act
            var loop = new VectorGraphFeedbackLoop(store, mettaEngine, embeddingModel, loopConfig);

            // Assert
            loop.Should().NotBeNull();
        }
        finally
        {
            store.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    #endregion

    #region ExecuteCycleAsync

    [Fact]
    public async Task ExecuteCycleAsync_NullDag_ReturnsFailure()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);
        var mettaEngine = new Mock<IMeTTaEngine>().Object;
        var embeddingModel = new Mock<IEmbeddingModel>().Object;
        var loop = new VectorGraphFeedbackLoop(store, mettaEngine, embeddingModel);

        try
        {
            // Act
            var result = await loop.ExecuteCycleAsync(null!);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be("DAG cannot be null");
        }
        finally
        {
            await store.DisposeAsync();
        }
    }

    [Fact]
    public async Task ExecuteCycleAsync_EmptyDag_ReturnsSuccess()
    {
        // Arrange
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);
        var mettaEngineMock = new Mock<IMeTTaEngine>();
        mettaEngineMock.Setup(e => e.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("[]"));
        var embeddingModelMock = new Mock<IEmbeddingModel>();
        embeddingModelMock.Setup(e => e.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[384]);
        var loop = new VectorGraphFeedbackLoop(store, mettaEngineMock.Object, embeddingModelMock.Object, new FeedbackLoopConfig(AutoPersist: false));
        var dag = new MerkleDag();

        try
        {
            // Act
            var result = await loop.ExecuteCycleAsync(dag);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.NodesAnalyzed.Should().Be(0);
        }
        finally
        {
            await store.DisposeAsync();
        }
    }

    #endregion
}

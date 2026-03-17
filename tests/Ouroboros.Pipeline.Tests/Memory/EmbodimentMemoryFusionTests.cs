using FluentAssertions;
using NSubstitute;
using Ouroboros.Core.Configuration;
using Qdrant.Client;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class EmbodimentMemoryFusionTests
{
    #region Constructor Tests (Registry-based)

    [Fact]
    public void Constructor_WithNullQdrantClient_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = Substitute.For<IQdrantCollectionRegistry>();
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var act = () => new EmbodimentMemoryFusion(null!, registry, embeddingModel);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("qdrantClient");
    }

    [Fact]
    public void Constructor_WithNullRegistry_ThrowsArgumentNullException()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var act = () => new EmbodimentMemoryFusion(qdrantClient, null!, embeddingModel);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("registry");

        qdrantClient.Dispose();
    }

    [Fact]
    public void Constructor_WithNullEmbeddingModel_ThrowsArgumentNullException()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var registry = Substitute.For<IQdrantCollectionRegistry>();
        registry.GetCollectionName(Arg.Any<QdrantCollectionRole>()).Returns("test_collection");

        // Act
        var act = () => new EmbodimentMemoryFusion(qdrantClient, registry, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("embeddingModel");

        qdrantClient.Dispose();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var registry = Substitute.For<IQdrantCollectionRegistry>();
        registry.GetCollectionName(QdrantCollectionRole.EmbodimentMemory).Returns("embodiment_memory");
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var fusion = new EmbodimentMemoryFusion(qdrantClient, registry, embeddingModel);

        // Assert
        fusion.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    #endregion

    #region Constructor Tests (Collection-name-based)

    [Fact]
    public void Constructor_WithExplicitCollectionName_CreatesInstance()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var fusion = new EmbodimentMemoryFusion(qdrantClient, embeddingModel, "custom_collection");

        // Assert
        fusion.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void Constructor_WithDefaultCollectionName_CreatesInstance()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var fusion = new EmbodimentMemoryFusion(qdrantClient, embeddingModel);

        // Assert
        fusion.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void Constructor_WithNullCollectionName_ThrowsArgumentNullException()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var act = () => new EmbodimentMemoryFusion(qdrantClient, embeddingModel, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("collectionName");

        qdrantClient.Dispose();
    }

    [Fact]
    public void Constructor_WithNullQdrantClientExplicit_ThrowsArgumentNullException()
    {
        // Arrange
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var act = () => new EmbodimentMemoryFusion(null!, embeddingModel, "collection");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("qdrantClient");
    }

    [Fact]
    public void Constructor_WithNullEmbeddingModelExplicit_ThrowsArgumentNullException()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);

        // Act
        var act = () => new EmbodimentMemoryFusion(qdrantClient, null!, "collection");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("embeddingModel");

        qdrantClient.Dispose();
    }

    #endregion

    #region IAsyncDisposable Tests

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();
        var fusion = new EmbodimentMemoryFusion(qdrantClient, embeddingModel, "test_collection");

        // Act
        var act = async () => await fusion.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();

        qdrantClient.Dispose();
    }

    [Fact]
    public void ImplementsIAsyncDisposable()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var fusion = new EmbodimentMemoryFusion(qdrantClient, embeddingModel);

        // Assert
        fusion.Should().BeAssignableTo<IAsyncDisposable>();

        qdrantClient.Dispose();
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void ImplementsIEmbodimentMemoryFusion()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var fusion = new EmbodimentMemoryFusion(qdrantClient, embeddingModel);

        // Assert
        fusion.Should().BeAssignableTo<IEmbodimentMemoryFusion>();

        qdrantClient.Dispose();
    }

    #endregion
}

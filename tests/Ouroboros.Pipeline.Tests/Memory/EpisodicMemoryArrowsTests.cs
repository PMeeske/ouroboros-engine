using FluentAssertions;
using NSubstitute;
using Ouroboros.Core.Configuration;
using Ouroboros.Pipeline.Verification;
using Qdrant.Client;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class EpisodicMemoryArrowsTests
{
    #region StoreEpisodeArrow Tests

    [Fact]
    public void StoreEpisodeArrow_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();
        var context = PipelineExecutionContext.WithGoal("test");
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var metadata = ImmutableDictionary<string, object>.Empty;

        // Act
        var step = EpisodicMemoryArrows.StoreEpisodeArrow(
            qdrantClient, embeddingModel, context, outcome, metadata);

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void StoreEpisodeArrow_WithCustomCollectionName_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();
        var context = PipelineExecutionContext.WithGoal("test");
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var metadata = ImmutableDictionary<string, object>.Empty;

        // Act
        var step = EpisodicMemoryArrows.StoreEpisodeArrow(
            qdrantClient, embeddingModel, context, outcome, metadata, "custom_collection");

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    #endregion

    #region SafeStoreEpisodeArrow Tests

    [Fact]
    public void SafeStoreEpisodeArrow_ReturnsNonNullArrow()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();
        var context = PipelineExecutionContext.WithGoal("test");
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var metadata = ImmutableDictionary<string, object>.Empty;

        // Act
        var arrow = EpisodicMemoryArrows.SafeStoreEpisodeArrow(
            qdrantClient, embeddingModel, context, outcome, metadata);

        // Assert
        arrow.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    #endregion

    #region RetrieveSimilarEpisodesArrow Tests

    [Fact]
    public void RetrieveSimilarEpisodesArrow_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var step = EpisodicMemoryArrows.RetrieveSimilarEpisodesArrow(
            qdrantClient, embeddingModel, "test query");

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void RetrieveSimilarEpisodesArrow_WithCustomParameters_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var step = EpisodicMemoryArrows.RetrieveSimilarEpisodesArrow(
            qdrantClient, embeddingModel, "query", topK: 10, minSimilarity: 0.5, collectionName: "custom");

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    #endregion

    #region CreateEpisodeRetriever Tests

    [Fact]
    public void CreateEpisodeRetriever_ReturnsNonNullFactory()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var factory = EpisodicMemoryArrows.CreateEpisodeRetriever(qdrantClient, embeddingModel);

        // Assert
        factory.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void CreateEpisodeRetriever_InvokedWithParameters_ReturnsStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        var factory = EpisodicMemoryArrows.CreateEpisodeRetriever(qdrantClient, embeddingModel);

        // Act
        var step = factory("test query", 5);

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void CreateEpisodeRetriever_WithCustomCollectionName_ReturnsNonNullFactory()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var factory = EpisodicMemoryArrows.CreateEpisodeRetriever(
            qdrantClient, embeddingModel, "custom_collection");

        // Assert
        factory.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    #endregion

    #region PlanWithExperienceArrow Tests

    [Fact]
    public void PlanWithExperienceArrow_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var step = EpisodicMemoryArrows.PlanWithExperienceArrow(
            qdrantClient, embeddingModel, "goal");

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void PlanWithExperienceArrow_WithCustomTopK_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var step = EpisodicMemoryArrows.PlanWithExperienceArrow(
            qdrantClient, embeddingModel, "goal", topK: 10);

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void PlanWithExperienceArrow_WithCustomCollectionName_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var step = EpisodicMemoryArrows.PlanWithExperienceArrow(
            qdrantClient, embeddingModel, "goal", collectionName: "custom");

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    #endregion

    #region CreateConfiguredMemorySystem Tests

    [Fact]
    public void CreateConfiguredMemorySystem_WithRegistry_ReturnsSystem()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var registry = Substitute.For<IQdrantCollectionRegistry>();
        registry.GetCollectionName(QdrantCollectionRole.EpisodicMemory).Returns("episodic_memory");
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var system = EpisodicMemoryArrows.CreateConfiguredMemorySystem(
            qdrantClient, registry, embeddingModel);

        // Assert
        system.Should().NotBeNull();
        system.Should().BeOfType<EpisodicMemorySystem>();

        qdrantClient.Dispose();
    }

    [Fact]
    public void CreateConfiguredMemorySystem_WithNullRegistry_ThrowsArgumentNullException()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var act = () => EpisodicMemoryArrows.CreateConfiguredMemorySystem(
            qdrantClient, null!, embeddingModel);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("registry");

        qdrantClient.Dispose();
    }

    [Fact]
    public void CreateConfiguredMemorySystem_UsesRegistryCollectionName()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var registry = Substitute.For<IQdrantCollectionRegistry>();
        registry.GetCollectionName(QdrantCollectionRole.EpisodicMemory).Returns("custom_episodic");
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var system = EpisodicMemoryArrows.CreateConfiguredMemorySystem(
            qdrantClient, registry, embeddingModel);

        // Assert
        system.Should().NotBeNull();
        registry.Received(1).GetCollectionName(QdrantCollectionRole.EpisodicMemory);

        qdrantClient.Dispose();
    }

    #pragma warning disable CS0618 // Obsolete
    [Fact]
    public void CreateConfiguredMemorySystem_ObsoleteOverload_ReturnsSystem()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var system = EpisodicMemoryArrows.CreateConfiguredMemorySystem(
            qdrantClient, embeddingModel, "my_collection");

        // Assert
        system.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void CreateConfiguredMemorySystem_ObsoleteOverloadDefaultName_ReturnsSystem()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var system = EpisodicMemoryArrows.CreateConfiguredMemorySystem(
            qdrantClient, embeddingModel);

        // Assert
        system.Should().NotBeNull();

        qdrantClient.Dispose();
    }
    #pragma warning restore CS0618

    #endregion
}

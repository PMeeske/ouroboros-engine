using FluentAssertions;
using NSubstitute;
using Ouroboros.Core.Configuration;
using Qdrant.Client;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class EpisodicMemorySystemTests
{
    [Fact]
    public void Constructor_WithNullQdrantClient_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = Substitute.For<IQdrantCollectionRegistry>();
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var act = () => new EpisodicMemorySystem(null!, registry, embeddingModel);

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
        var act = () => new EpisodicMemorySystem(qdrantClient, null!, embeddingModel);

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
        var act = () => new EpisodicMemorySystem(qdrantClient, registry, null!);

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
        registry.GetCollectionName(QdrantCollectionRole.EpisodicMemory).Returns("episodic_memory");
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        // Act
        var system = new EpisodicMemorySystem(qdrantClient, registry, embeddingModel);

        // Assert
        system.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void StoreEpisode_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var registry = Substitute.For<IQdrantCollectionRegistry>();
        registry.GetCollectionName(QdrantCollectionRole.EpisodicMemory).Returns("test_collection");
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        var system = new EpisodicMemorySystem(qdrantClient, registry, embeddingModel);
        var context = PipelineExecutionContext.WithGoal("test");
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var metadata = ImmutableDictionary<string, object>.Empty;

        // Act
        var step = system.StoreEpisode(context, outcome, metadata);

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void RetrieveSimilarEpisodes_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var registry = Substitute.For<IQdrantCollectionRegistry>();
        registry.GetCollectionName(QdrantCollectionRole.EpisodicMemory).Returns("test_collection");
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        var system = new EpisodicMemorySystem(qdrantClient, registry, embeddingModel);

        // Act
        var step = system.RetrieveSimilarEpisodes("query");

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void RetrieveSimilarEpisodes_WithCustomParameters_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var registry = Substitute.For<IQdrantCollectionRegistry>();
        registry.GetCollectionName(QdrantCollectionRole.EpisodicMemory).Returns("test_collection");
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        var system = new EpisodicMemorySystem(qdrantClient, registry, embeddingModel);

        // Act
        var step = system.RetrieveSimilarEpisodes("query", topK: 10, minSimilarity: 0.5);

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void PlanWithExperience_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var registry = Substitute.For<IQdrantCollectionRegistry>();
        registry.GetCollectionName(QdrantCollectionRole.EpisodicMemory).Returns("test_collection");
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        var system = new EpisodicMemorySystem(qdrantClient, registry, embeddingModel);

        // Act
        var step = system.PlanWithExperience("goal");

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }

    [Fact]
    public void PlanWithExperience_WithCustomTopK_ReturnsNonNullStep()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var registry = Substitute.For<IQdrantCollectionRegistry>();
        registry.GetCollectionName(QdrantCollectionRole.EpisodicMemory).Returns("test_collection");
        var embeddingModel = Substitute.For<IEmbeddingModel>();

        var system = new EpisodicMemorySystem(qdrantClient, registry, embeddingModel);

        // Act
        var step = system.PlanWithExperience("goal", topK: 10);

        // Assert
        step.Should().NotBeNull();

        qdrantClient.Dispose();
    }
}

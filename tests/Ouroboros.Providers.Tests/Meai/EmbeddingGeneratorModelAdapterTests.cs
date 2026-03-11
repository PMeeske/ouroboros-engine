using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Ouroboros.Providers.Meai;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

namespace Ouroboros.Tests.Meai;

[Trait("Category", "Unit")]
public sealed class EmbeddingGeneratorModelAdapterTests
{
    [Fact]
    public void Constructor_NullGenerator_ThrowsArgumentNullException()
    {
        // Arrange
        IEmbeddingGenerator<string, Embedding<float>> generator = null!;

        // Act
        var act = () => new EmbeddingGeneratorModelAdapter(generator);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_ReturnsVectorFromGenerator()
    {
        // Arrange
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var embedding = new Embedding<float>(new float[] { 1.0f, 2.0f, 3.0f });
        var embeddings = new GeneratedEmbeddings<Embedding<float>>(new[] { embedding });
        mockGenerator.Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);

        var sut = new EmbeddingGeneratorModelAdapter(mockGenerator.Object);

        // Act
        var result = await sut.CreateEmbeddingsAsync("test input");

        // Assert
        result.Should().BeEquivalentTo(new[] { 1.0f, 2.0f, 3.0f });
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_NoResults_ReturnsEmptyArray()
    {
        // Arrange
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var embeddings = new GeneratedEmbeddings<Embedding<float>>(new List<Embedding<float>>());
        mockGenerator.Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);

        var sut = new EmbeddingGeneratorModelAdapter(mockGenerator.Object);

        // Act
        var result = await sut.CreateEmbeddingsAsync("test input");

        // Assert
        result.Should().BeEmpty();
    }
}

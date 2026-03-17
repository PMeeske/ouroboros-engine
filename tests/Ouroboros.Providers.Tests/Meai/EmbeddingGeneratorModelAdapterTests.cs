using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Ouroboros.Providers.Meai;
using Xunit;

namespace Ouroboros.Tests.Meai;

[Trait("Category", "Unit")]
public sealed class EmbeddingGeneratorModelAdapterTests
{
    [Fact]
    public void Ctor_WithNullGenerator_ThrowsArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() => new EmbeddingGeneratorModelAdapter(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithValidInput_ReturnsEmbedding()
    {
        // Arrange
        var expectedVector = new float[] { 0.1f, 0.2f, 0.3f };
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator
            .Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(
                new List<Embedding<float>> { new Embedding<float>(expectedVector) }));

        var sut = new EmbeddingGeneratorModelAdapter(mockGenerator.Object);

        // Act
        var result = await sut.CreateEmbeddingsAsync("test input");

        // Assert
        result.Should().BeEquivalentTo(expectedVector);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithEmptyResult_ReturnsEmptyArray()
    {
        // Arrange
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator
            .Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(new List<Embedding<float>>()));

        var sut = new EmbeddingGeneratorModelAdapter(mockGenerator.Object);

        // Act
        var result = await sut.CreateEmbeddingsAsync("test");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_PassesCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator
            .Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.Is<CancellationToken>(ct => ct.IsCancellationRequested)))
            .ThrowsAsync(new OperationCanceledException());

        var sut = new EmbeddingGeneratorModelAdapter(mockGenerator.Object);

        // Act & Assert
        await FluentActions.Invoking(() => sut.CreateEmbeddingsAsync("test", cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_PassesInputAsArray()
    {
        // Arrange
        IEnumerable<string>? capturedInput = null;
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator
            .Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, EmbeddingGenerationOptions?, CancellationToken>(
                (input, _, _) => capturedInput = input)
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(
                new List<Embedding<float>> { new Embedding<float>(new float[] { 1.0f }) }));

        var sut = new EmbeddingGeneratorModelAdapter(mockGenerator.Object);

        // Act
        await sut.CreateEmbeddingsAsync("my input text");

        // Assert
        capturedInput.Should().NotBeNull();
        capturedInput.Should().ContainSingle().Which.Should().Be("my input text");
    }
}

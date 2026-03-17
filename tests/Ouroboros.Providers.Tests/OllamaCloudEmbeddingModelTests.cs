using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class OllamaCloudEmbeddingModelTests
{
    [Fact]
    public void Ctor_WithNullEndpoint_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new OllamaCloudEmbeddingModel(null!, "key", "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithEmptyEndpoint_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new OllamaCloudEmbeddingModel("", "key", "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithNullApiKey_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new OllamaCloudEmbeddingModel("http://localhost:11434", null!, "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithEmptyApiKey_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new OllamaCloudEmbeddingModel("http://localhost:11434", "", "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithValidArgs_DoesNotThrow()
    {
        FluentActions.Invoking(() => new OllamaCloudEmbeddingModel(
            "http://localhost:11434", "test-key", "nomic-embed-text"))
            .Should().NotThrow();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WhenEndpointUnavailable_ReturnsFallback()
    {
        // Arrange - Use unreachable endpoint
        var sut = new OllamaCloudEmbeddingModel("http://localhost:1", "test-key", "nomic-embed-text");

        // Act - Should fall back to deterministic embedding
        var result = await sut.CreateEmbeddingsAsync("test input");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = new OllamaCloudEmbeddingModel("http://localhost:11434", "key", "model");
        FluentActions.Invoking(() => sut.Dispose()).Should().NotThrow();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_DeterministicFallback_ReturnsSameForSameInput()
    {
        // Arrange - Unreachable endpoint forces fallback
        var sut = new OllamaCloudEmbeddingModel("http://localhost:1", "test-key", "model");

        // Act
        var result1 = await sut.CreateEmbeddingsAsync("test input");
        var result2 = await sut.CreateEmbeddingsAsync("test input");

        // Assert - Deterministic fallback should produce same result
        result1.Should().BeEquivalentTo(result2);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_DeterministicFallback_DifferentInputsDifferentResults()
    {
        // Arrange
        var sut = new OllamaCloudEmbeddingModel("http://localhost:1", "test-key", "model");

        // Act
        var result1 = await sut.CreateEmbeddingsAsync("input one");
        var result2 = await sut.CreateEmbeddingsAsync("input two");

        // Assert - Different inputs should produce different embeddings
        result1.Should().NotBeEquivalentTo(result2);
    }
}

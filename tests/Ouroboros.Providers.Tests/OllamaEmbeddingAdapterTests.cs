using FluentAssertions;
using Moq;
using OllamaSharp;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class OllamaEmbeddingAdapterTests
{
    [Fact]
    public void Ctor_WithNullClient_ThrowsArgumentNullException()
    {
        FluentActions.Invoking(() => new OllamaEmbeddingAdapter(null!, "model"))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_WithNullModelName_ThrowsArgumentException()
    {
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        FluentActions.Invoking(() => new OllamaEmbeddingAdapter(client, null!))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithEmptyModelName_ThrowsArgumentException()
    {
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        FluentActions.Invoking(() => new OllamaEmbeddingAdapter(client, ""))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithWhitespaceModelName_ThrowsArgumentException()
    {
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        FluentActions.Invoking(() => new OllamaEmbeddingAdapter(client, "   "))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithValidArgs_DoesNotThrow()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));

        // Act & Assert
        FluentActions.Invoking(() => new OllamaEmbeddingAdapter(client, "nomic-embed-text"))
            .Should().NotThrow();
    }

    [Fact]
    public void GetEmbeddingGenerator_ReturnsClient()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        var sut = new OllamaEmbeddingAdapter(client, "nomic-embed-text");

        // Act
        var generator = sut.GetEmbeddingGenerator();

        // Assert
        generator.Should().BeSameAs(client);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WhenOllamaUnavailable_ReturnsFallback()
    {
        // Arrange - Use a non-existent endpoint to trigger fallback
        var client = new OllamaApiClient(new Uri("http://localhost:1"));
        var sut = new OllamaEmbeddingAdapter(client, "nomic-embed-text");

        // Act - Should use deterministic fallback without throwing
        var result = await sut.CreateEmbeddingsAsync("test input");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithEmptyInput_ReturnsFallback()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:1"));
        var sut = new OllamaEmbeddingAdapter(client, "nomic-embed-text");

        // Act
        var result = await sut.CreateEmbeddingsAsync("");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithNullInput_ReturnsFallback()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:1"));
        var sut = new OllamaEmbeddingAdapter(client, "nomic-embed-text");

        // Act
        var result = await sut.CreateEmbeddingsAsync(null!);

        // Assert - Sanitization should handle null input
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithSpecialCharacters_ReturnsFallback()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:1"));
        var sut = new OllamaEmbeddingAdapter(client, "nomic-embed-text");

        // Act - Input with characters that get sanitized
        var result = await sut.CreateEmbeddingsAsync("Hello \0 world \ud800");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithVeryLongInput_TruncatesAndReturns()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:1"));
        var sut = new OllamaEmbeddingAdapter(client, "nomic-embed-text");
        var longInput = new string('A', 10000);

        // Act
        var result = await sut.CreateEmbeddingsAsync(longInput);

        // Assert - Should still work after truncation
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }
}

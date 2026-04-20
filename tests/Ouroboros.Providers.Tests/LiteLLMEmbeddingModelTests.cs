using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class LiteLLMEmbeddingModelTests
{
    [Fact]
    public void Ctor_WithNullEndpoint_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new LiteLLMEmbeddingModel(null!, "key", "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithEmptyEndpoint_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new LiteLLMEmbeddingModel("", "key", "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithNullApiKey_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new LiteLLMEmbeddingModel("http://localhost:4000", null!, "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithEmptyApiKey_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new LiteLLMEmbeddingModel("http://localhost:4000", "", "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithValidArgs_DoesNotThrow()
    {
        FluentActions.Invoking(() => new LiteLLMEmbeddingModel(
            "http://localhost:4000", "test-key", "text-embedding-ada-002"))
            .Should().NotThrow();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WhenEndpointUnavailable_ReturnsFallback()
    {
        // Arrange
        var sut = new LiteLLMEmbeddingModel("http://localhost:1", "test-key", "model");

        // Act
        var result = await sut.CreateEmbeddingsAsync("test input");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = new LiteLLMEmbeddingModel("http://localhost:4000", "key", "model");
        FluentActions.Invoking(() => sut.Dispose()).Should().NotThrow();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_DeterministicFallback_ConsistentResults()
    {
        // Arrange
        var sut = new LiteLLMEmbeddingModel("http://localhost:1", "key", "model");

        // Act
        var r1 = await sut.CreateEmbeddingsAsync("test");
        var r2 = await sut.CreateEmbeddingsAsync("test");

        // Assert
        r1.Should().BeEquivalentTo(r2);
    }
}

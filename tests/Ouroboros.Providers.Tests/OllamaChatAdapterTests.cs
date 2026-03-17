using FluentAssertions;
using OllamaSharp;
using OllamaSharp.Models;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class OllamaChatAdapterTests
{
    [Fact]
    public void Ctor_WithNullClient_ThrowsArgumentNullException()
    {
        FluentActions.Invoking(() => new OllamaChatAdapter(null!, "model"))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_WithNullModelName_ThrowsArgumentException()
    {
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        FluentActions.Invoking(() => new OllamaChatAdapter(client, null!))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithEmptyModelName_ThrowsArgumentException()
    {
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        FluentActions.Invoking(() => new OllamaChatAdapter(client, ""))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithValidArgs_DoesNotThrow()
    {
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        FluentActions.Invoking(() => new OllamaChatAdapter(client, "llama3"))
            .Should().NotThrow();
    }

    [Fact]
    public void Options_DefaultsToNull()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        var sut = new OllamaChatAdapter(client, "llama3");

        // Assert
        sut.Options.Should().BeNull();
    }

    [Fact]
    public void Options_CanBeSet()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        var sut = new OllamaChatAdapter(client, "llama3");
        var options = new RequestOptions { Temperature = 0.5f };

        // Act
        sut.Options = options;

        // Assert
        sut.Options.Should().BeSameAs(options);
    }

    [Fact]
    public void KeepAlive_DefaultsToNull()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        var sut = new OllamaChatAdapter(client, "llama3");

        // Assert
        sut.KeepAlive.Should().BeNull();
    }

    [Fact]
    public void KeepAlive_CanBeSet()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        var sut = new OllamaChatAdapter(client, "llama3");

        // Act
        sut.KeepAlive = "10m";

        // Assert
        sut.KeepAlive.Should().Be("10m");
    }

    [Fact]
    public void GetChatClient_ReturnsOllamaClient()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        var sut = new OllamaChatAdapter(client, "llama3");

        // Act
        var chatClient = sut.GetChatClient();

        // Assert
        chatClient.Should().BeSameAs(client);
    }

    [Fact]
    public async Task GenerateTextAsync_WhenOllamaUnavailable_ReturnsFallback()
    {
        // Arrange - non-existent endpoint to trigger fallback
        var client = new OllamaApiClient(new Uri("http://localhost:1"));
        var sut = new OllamaChatAdapter(client, "llama3");

        // Act
        var result = await sut.GenerateTextAsync("Hello");

        // Assert - Should return fallback message
        result.Should().Contain("ollama-fallback");
        result.Should().Contain("llama3");
        result.Should().Contain("Hello");
    }

    [Fact]
    public async Task GenerateWithThinkingAsync_WhenOllamaUnavailable_ReturnsFallbackResponse()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:1"));
        var sut = new OllamaChatAdapter(client, "llama3");

        // Act
        var result = await sut.GenerateWithThinkingAsync("Test prompt");

        // Assert - Should parse fallback text through ThinkingResponse.FromRawText
        result.Should().NotBeNull();
        result.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateTextAsync_WithCulture_IncludesCultureInFallback()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:1"));
        var sut = new OllamaChatAdapter(client, "llama3", "German");

        // Act
        var result = await sut.GenerateTextAsync("Hello");

        // Assert - Fallback includes the culture-modified prompt
        result.Should().Contain("ollama-fallback");
        result.Should().Contain("German");
    }

    [Fact]
    public void StreamWithThinkingAsync_ReturnsObservable()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        var sut = new OllamaChatAdapter(client, "llama3");

        // Act
        var observable = sut.StreamWithThinkingAsync("test prompt");

        // Assert
        observable.Should().NotBeNull();
    }

    [Fact]
    public void StreamReasoningContent_ReturnsObservable()
    {
        // Arrange
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        var sut = new OllamaChatAdapter(client, "llama3");

        // Act
        var observable = sut.StreamReasoningContent("test prompt");

        // Assert
        observable.Should().NotBeNull();
    }
}

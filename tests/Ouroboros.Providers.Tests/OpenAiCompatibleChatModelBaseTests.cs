using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class OpenAiCompatibleChatModelBaseTests
{
    [Fact]
    public void Ctor_WithNullEndpoint_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new TestChatModel(null!, "key", "model", "Test"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithEmptyEndpoint_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new TestChatModel("", "key", "model", "Test"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithNullApiKey_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new TestChatModel("http://localhost:8080", null!, "model", "Test"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithEmptyApiKey_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new TestChatModel("http://localhost:8080", "", "model", "Test"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithValidArgs_DoesNotThrow()
    {
        FluentActions.Invoking(() => new TestChatModel("http://localhost:8080", "key", "model", "Test"))
            .Should().NotThrow();
    }

    [Fact]
    public void CostTracker_IsInitialized()
    {
        using var sut = new TestChatModel("http://localhost:8080", "key", "model", "Test");
        sut.CostTracker.Should().NotBeNull();
    }

    [Fact]
    public void CostTracker_UsesProvidedTracker()
    {
        var tracker = new LlmCostTracker("test-model", "Test");
        using var sut = new TestChatModel("http://localhost:8080", "key", "model", "Test", costTracker: tracker);
        sut.CostTracker.Should().BeSameAs(tracker);
    }

    [Fact]
    public async Task GenerateWithThinkingAsync_WhenEndpointUnavailable_ReturnsFallback()
    {
        // Arrange
        using var sut = new TestChatModel("http://localhost:1", "key", "test-model", "TestProvider");

        // Act
        var result = await sut.GenerateWithThinkingAsync("test prompt");

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("testprovider-fallback");
        result.Content.Should().Contain("test-model");
    }

    [Fact]
    public async Task GenerateTextAsync_WhenEndpointUnavailable_ReturnsFallback()
    {
        // Arrange
        using var sut = new TestChatModel("http://localhost:1", "key", "test-model", "TestProvider");

        // Act
        var result = await sut.GenerateTextAsync("test prompt");

        // Assert
        result.Should().Contain("testprovider-fallback");
    }

    [Fact]
    public void StreamWithThinkingAsync_ReturnsObservable()
    {
        using var sut = new TestChatModel("http://localhost:8080", "key", "model", "Test");
        var observable = sut.StreamWithThinkingAsync("test");
        observable.Should().NotBeNull();
    }

    [Fact]
    public void StreamReasoningContent_ReturnsObservable()
    {
        using var sut = new TestChatModel("http://localhost:8080", "key", "model", "Test");
        var observable = sut.StreamReasoningContent("test");
        observable.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = new TestChatModel("http://localhost:8080", "key", "model", "Test");
        FluentActions.Invoking(() => sut.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void GetFallbackMessage_CanBeOverridden()
    {
        using var sut = new CustomFallbackChatModel("http://localhost:1", "key", "model", "Custom");
        // Custom fallback is exposed via GenerateWithThinking fallback
        var result = sut.GenerateWithThinkingAsync("test").GetAwaiter().GetResult();
        result.Content.Should().Contain("custom-fallback");
    }

    /// <summary>
    /// Concrete test subclass of the abstract base.
    /// </summary>
    private sealed class TestChatModel : OpenAiCompatibleChatModelBase
    {
        public TestChatModel(string endpoint, string apiKey, string model, string providerName,
            ChatRuntimeSettings? settings = null, LlmCostTracker? costTracker = null)
            : base(endpoint, apiKey, model, providerName, settings, costTracker)
        {
        }
    }

    /// <summary>
    /// Test subclass with custom fallback message.
    /// </summary>
    private sealed class CustomFallbackChatModel : OpenAiCompatibleChatModelBase
    {
        public CustomFallbackChatModel(string endpoint, string apiKey, string model, string providerName)
            : base(endpoint, apiKey, model, providerName)
        {
        }

        protected override string GetFallbackMessage(string prompt) => $"[custom-fallback] {prompt}";
    }
}

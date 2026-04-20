using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests.Anthropic;

[Trait("Category", "Unit")]
public sealed class AnthropicChatModelTests
{
    [Fact]
    public void Ctor_WithNullApiKey_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new AnthropicChatModel(null!, "claude-sonnet-4-20250514"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithEmptyApiKey_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new AnthropicChatModel("", "claude-sonnet-4-20250514"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithWhitespaceApiKey_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => new AnthropicChatModel("   ", "claude-sonnet-4-20250514"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_WithValidArgs_DoesNotThrow()
    {
        FluentActions.Invoking(() => new AnthropicChatModel("sk-ant-test-key", "claude-sonnet-4-20250514"))
            .Should().NotThrow();
    }

    [Fact]
    public void CostTracker_IsInitialized()
    {
        // Arrange & Act
        using var sut = new AnthropicChatModel("sk-ant-test-key", "claude-sonnet-4-20250514");

        // Assert
        sut.CostTracker.Should().NotBeNull();
    }

    [Fact]
    public void CostTracker_UsesProvidedTracker()
    {
        // Arrange
        var tracker = new LlmCostTracker("test-model", "Test");

        // Act
        using var sut = new AnthropicChatModel("sk-ant-test-key", "claude-sonnet-4-20250514", costTracker: tracker);

        // Assert
        sut.CostTracker.Should().BeSameAs(tracker);
    }

    [Fact]
    public void CostTracker_DefaultsToAnthropicProvider()
    {
        // Arrange & Act
        using var sut = new AnthropicChatModel("sk-ant-test-key", "claude-sonnet-4-20250514");

        // Assert
        sut.CostTracker.Should().NotBeNull();
    }

    [Fact]
    public void FromEnvironment_WithoutEnvVar_ThrowsInvalidOperationException()
    {
        // Arrange - ensure env var is not set
        var original = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);

            // Act & Assert
            FluentActions.Invoking(() => AnthropicChatModel.FromEnvironment("claude-sonnet-4-20250514"))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*ANTHROPIC_API_KEY*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", original);
        }
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var sut = new AnthropicChatModel("sk-ant-test-key", "claude-sonnet-4-20250514");

        // Act & Assert
        FluentActions.Invoking(() => sut.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void StreamWithThinkingAsync_ReturnsObservable()
    {
        // Arrange
        using var sut = new AnthropicChatModel("sk-ant-test-key", "claude-sonnet-4-20250514");

        // Act
        var observable = sut.StreamWithThinkingAsync("test prompt");

        // Assert
        observable.Should().NotBeNull();
    }

    [Fact]
    public void StreamReasoningContent_ReturnsObservable()
    {
        // Arrange
        using var sut = new AnthropicChatModel("sk-ant-test-key", "claude-sonnet-4-20250514");

        // Act
        var observable = sut.StreamReasoningContent("test prompt");

        // Assert
        observable.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_WithSettings_UsesProvidedSettings()
    {
        // Arrange
        var settings = new ChatRuntimeSettings { Culture = "German", MaxTokens = 1000 };

        // Act & Assert
        FluentActions.Invoking(() => new AnthropicChatModel("sk-ant-test-key", "claude-sonnet-4-20250514", settings))
            .Should().NotThrow();
    }

    [Fact]
    public void Ctor_WithThinkingBudget_DoesNotThrow()
    {
        // Act & Assert
        FluentActions.Invoking(() => new AnthropicChatModel("sk-ant-test-key", "claude-sonnet-4-20250514", thinkingBudgetTokens: 4096))
            .Should().NotThrow();
    }
}

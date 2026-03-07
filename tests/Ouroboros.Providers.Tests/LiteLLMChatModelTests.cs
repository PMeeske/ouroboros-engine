namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class LiteLLMChatModelTests
{
    [Fact]
    public void Ctor_WithValidArgs_DoesNotThrow()
    {
        var model = new LiteLLMChatModel("http://localhost:4000", "test-key", "gpt-4o");

        model.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_WithSettings_DoesNotThrow()
    {
        var settings = new ChatRuntimeSettings();
        var model = new LiteLLMChatModel("http://localhost:4000", "test-key", "gpt-4o", settings);

        model.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_WithCostTracker_DoesNotThrow()
    {
        var tracker = new LlmCostTracker("gpt-4o", "openai");
        var model = new LiteLLMChatModel("http://localhost:4000", "test-key", "gpt-4o", costTracker: tracker);

        model.Should().NotBeNull();
    }
}

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class OllamaCloudChatModelTests
{
    [Fact]
    public void Ctor_NullEndpoint_Throws()
    {
        FluentActions.Invoking(() => new OllamaCloudChatModel(null!, "key", "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_EmptyEndpoint_Throws()
    {
        FluentActions.Invoking(() => new OllamaCloudChatModel("", "key", "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NullApiKey_Throws()
    {
        FluentActions.Invoking(() => new OllamaCloudChatModel("http://localhost:11434", null!, "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_EmptyApiKey_Throws()
    {
        FluentActions.Invoking(() => new OllamaCloudChatModel("http://localhost:11434", "", "model"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_ValidArgs_DoesNotThrow()
    {
        var model = new OllamaCloudChatModel("http://localhost:11434", "test-key", "deepseek-r1:32b");

        model.Should().NotBeNull();
        model.Dispose();
    }

    [Fact]
    public void Ctor_WithCostTracker_DoesNotThrow()
    {
        var tracker = new LlmCostTracker("deepseek-r1:32b");
        var model = new OllamaCloudChatModel(
            "http://localhost:11434", "test-key", "deepseek-r1:32b", costTracker: tracker);

        model.CostTracker.Should().BeSameAs(tracker);
        model.Dispose();
    }

    [Fact]
    public void Ctor_WithoutCostTracker_CreatesDefault()
    {
        var model = new OllamaCloudChatModel("http://localhost:11434", "test-key", "deepseek-r1:32b");

        model.CostTracker.Should().NotBeNull();
        model.Dispose();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var model = new OllamaCloudChatModel("http://localhost:11434", "test-key", "model");

        FluentActions.Invoking(() => model.Dispose()).Should().NotThrow();
    }
}

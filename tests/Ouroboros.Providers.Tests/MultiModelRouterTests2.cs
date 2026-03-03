using FluentAssertions;
using Moq;
using Ouroboros.Providers;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public sealed class MultiModelRouterTests2
{
    [Fact]
    public void Constructor_EmptyModels_Throws()
    {
        var act = () => new MultiModelRouter(
            new Dictionary<string, IChatCompletionModel>(),
            "fallback");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task GenerateTextAsync_CodePrompt_RoutesToCoder()
    {
        var coderMock = new Mock<IChatCompletionModel>();
        coderMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("code output");

        var fallbackMock = new Mock<IChatCompletionModel>();

        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["coder"] = coderMock.Object,
            ["fallback"] = fallbackMock.Object,
        };

        var router = new MultiModelRouter(models, "fallback");
        var result = await router.GenerateTextAsync("Write code for sorting");

        result.Should().Be("code output");
    }

    [Fact]
    public async Task GenerateTextAsync_LongPrompt_RoutesToSummarizer()
    {
        var summarizeMock = new Mock<IChatCompletionModel>();
        summarizeMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("summary");

        var fallbackMock = new Mock<IChatCompletionModel>();

        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["summarize"] = summarizeMock.Object,
            ["fallback"] = fallbackMock.Object,
        };

        var router = new MultiModelRouter(models, "fallback");
        var longPrompt = new string('x', 700);
        var result = await router.GenerateTextAsync(longPrompt);

        result.Should().Be("summary");
    }

    [Fact]
    public async Task GenerateTextAsync_ReasoningPrompt_RoutesToReasoner()
    {
        var reasonMock = new Mock<IChatCompletionModel>();
        reasonMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("reasoned");

        var fallbackMock = new Mock<IChatCompletionModel>();

        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["reason"] = reasonMock.Object,
            ["fallback"] = fallbackMock.Object,
        };

        var router = new MultiModelRouter(models, "fallback");
        var result = await router.GenerateTextAsync("Please reason about this");

        result.Should().Be("reasoned");
    }

    [Fact]
    public async Task GenerateTextAsync_EmptyPrompt_RoutesToFallback()
    {
        var fallbackMock = new Mock<IChatCompletionModel>();
        fallbackMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback answer");

        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["fallback"] = fallbackMock.Object,
        };

        var router = new MultiModelRouter(models, "fallback");
        var result = await router.GenerateTextAsync("");

        result.Should().Be("fallback answer");
    }

    [Fact]
    public async Task GenerateTextAsync_NullPrompt_RoutesToFallback()
    {
        var fallbackMock = new Mock<IChatCompletionModel>();
        fallbackMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback");

        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["fallback"] = fallbackMock.Object,
        };

        var router = new MultiModelRouter(models, "fallback");
        var result = await router.GenerateTextAsync(null!);

        result.Should().Be("fallback");
    }

    [Fact]
    public async Task GenerateTextAsync_UnknownPrompt_UsesFirstModel()
    {
        var firstMock = new Mock<IChatCompletionModel>();
        firstMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("first model");

        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["other"] = firstMock.Object,
        };

        var router = new MultiModelRouter(models, "missing-fallback");
        var result = await router.GenerateTextAsync("Hello");

        result.Should().Be("first model");
    }
}

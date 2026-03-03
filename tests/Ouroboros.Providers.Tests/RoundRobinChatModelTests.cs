using FluentAssertions;
using Moq;
using Ouroboros.Providers;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public sealed class RoundRobinChatModelTests : IDisposable
{
    private readonly RoundRobinChatModel _sut;

    public RoundRobinChatModelTests()
    {
        _sut = new RoundRobinChatModel(failoverEnabled: true, maxRetries: 3);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void Constructor_Defaults()
    {
        using var model = new RoundRobinChatModel();
        model.ActiveProviderCount.Should().Be(0);
        model.CostTracker.Should().NotBeNull();
    }

    [Fact]
    public void AddProvider_IncreasesActiveCount()
    {
        var mockModel = CreateMockModel("response");
        _sut.AddProvider(mockModel.Object, new ProviderConfig("Test", ChatEndpointType.OllamaLocal));

        _sut.ActiveProviderCount.Should().Be(1);
    }

    [Fact]
    public async Task GenerateTextAsync_NoProviders_Throws()
    {
        var act = () => _sut.GenerateTextAsync("hello");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No available providers*");
    }

    [Fact]
    public async Task GenerateTextAsync_SingleProvider_ReturnsResponse()
    {
        var mockModel = CreateMockModel("hello world");
        _sut.AddProvider(mockModel.Object, new ProviderConfig("Test", ChatEndpointType.OllamaLocal));

        var result = await _sut.GenerateTextAsync("test");

        result.Should().Be("hello world");
    }

    [Fact]
    public async Task GenerateTextAsync_Failover_TriesNextProvider()
    {
        var failMock = new Mock<IChatCompletionModel>();
        failMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("down"));

        var successMock = CreateMockModel("recovered");

        _sut.AddProvider(failMock.Object, new ProviderConfig("Fail", ChatEndpointType.OllamaLocal));
        _sut.AddProvider(successMock.Object, new ProviderConfig("Success", ChatEndpointType.OllamaLocal));

        var result = await _sut.GenerateTextAsync("test");

        result.Should().Be("recovered");
    }

    [Fact]
    public async Task GenerateTextAsync_AllFail_ThrowsWithLastException()
    {
        var fail1 = new Mock<IChatCompletionModel>();
        fail1.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail1"));

        var fail2 = new Mock<IChatCompletionModel>();
        fail2.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail2"));

        _sut.AddProvider(fail1.Object, new ProviderConfig("F1", ChatEndpointType.OllamaLocal));
        _sut.AddProvider(fail2.Object, new ProviderConfig("F2", ChatEndpointType.OllamaLocal));

        var act = () => _sut.GenerateTextAsync("test");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*providers failed*");
    }

    [Fact]
    public async Task GenerateTextAsync_CancellationRequested_PropagatesImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockModel = new Mock<IChatCompletionModel>();
        mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        _sut.AddProvider(mockModel.Object, new ProviderConfig("Test", ChatEndpointType.OllamaLocal));

        var act = () => _sut.GenerateTextAsync("test", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateTextAsync_FallbackResponse_TriggersFailover()
    {
        var fallbackModel = new Mock<IChatCompletionModel>();
        fallbackModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("some-fallback: model unavailable");

        var successModel = CreateMockModel("real answer");

        _sut.AddProvider(fallbackModel.Object, new ProviderConfig("Fallback", ChatEndpointType.OllamaLocal));
        _sut.AddProvider(successModel.Object, new ProviderConfig("Success", ChatEndpointType.OllamaLocal));

        var result = await _sut.GenerateTextAsync("test");

        result.Should().Be("real answer");
    }

    [Fact]
    public async Task ProviderStatistics_TracksSuccessAndFailure()
    {
        var mockModel = CreateMockModel("response");
        _sut.AddProvider(mockModel.Object, new ProviderConfig("Test", ChatEndpointType.OllamaLocal));

        await _sut.GenerateTextAsync("test");

        _sut.ProviderStatistics.Should().HaveCount(1);
        _sut.ProviderStatistics[0].Name.Should().Be("Test");
    }

    [Fact]
    public void ResetHealth_ResetsConsecutiveFailures()
    {
        var mockModel = CreateMockModel("ok");
        _sut.AddProvider(mockModel.Object, new ProviderConfig("Test", ChatEndpointType.OllamaLocal));

        _sut.ResetHealth();

        _sut.ActiveProviderCount.Should().Be(1);
    }

    [Fact]
    public void GetStatusSummary_EmptyPool_ReturnsMessage()
    {
        var summary = _sut.GetStatusSummary();
        summary.Should().Contain("No providers configured");
    }

    [Fact]
    public void GetStatusSummary_WithProviders_ReturnsFormatted()
    {
        var mockModel = CreateMockModel("ok");
        _sut.AddProvider(mockModel.Object, new ProviderConfig("Test", ChatEndpointType.OllamaLocal));

        var summary = _sut.GetStatusSummary();

        summary.Should().Contain("Round-Robin Pool");
        summary.Should().Contain("Test");
    }

    [Fact]
    public async Task GenerateTextAsync_FailoverDisabled_ThrowsOnFirstFailure()
    {
        using var noFailover = new RoundRobinChatModel(failoverEnabled: false);

        var failMock = new Mock<IChatCompletionModel>();
        failMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("first fail"));

        noFailover.AddProvider(failMock.Object, new ProviderConfig("Test", ChatEndpointType.OllamaLocal));
        noFailover.AddProvider(CreateMockModel("backup").Object, new ProviderConfig("Backup", ChatEndpointType.OllamaLocal));

        var act = () => noFailover.GenerateTextAsync("test");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("first fail");
    }

    [Fact]
    public void Dispose_ClearsProviders()
    {
        var mockModel = CreateMockModel("ok");
        _sut.AddProvider(mockModel.Object, new ProviderConfig("Test", ChatEndpointType.OllamaLocal));

        _sut.Dispose();

        _sut.ActiveProviderCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_DisposesDisposableModels()
    {
        var disposableMock = new Mock<IChatCompletionModel>();
        disposableMock.As<IDisposable>();
        _sut.AddProvider(disposableMock.Object, new ProviderConfig("Test", ChatEndpointType.OllamaLocal));

        _sut.Dispose();

        disposableMock.As<IDisposable>().Verify(d => d.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GenerateWithThinkingAsync_NonThinkingModel_WrapsInThinkingResponse()
    {
        var mockModel = CreateMockModel("simple text");
        _sut.AddProvider(mockModel.Object, new ProviderConfig("Test", ChatEndpointType.OllamaLocal));

        var result = await _sut.GenerateWithThinkingAsync("test");

        result.Content.Should().Be("simple text");
        result.HasThinking.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWithThinkingAsync_ThinkingModel_ReturnsThinkingResponse()
    {
        var thinkingMock = new Mock<IChatCompletionModel>();
        thinkingMock.As<IThinkingChatModel>()
            .Setup(m => m.GenerateWithThinkingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThinkingResponse("thoughts", "answer"));

        _sut.AddProvider(thinkingMock.Object, new ProviderConfig("Thinking", ChatEndpointType.OllamaLocal));

        var result = await _sut.GenerateWithThinkingAsync("test");

        result.Thinking.Should().Be("thoughts");
        result.Content.Should().Be("answer");
    }

    private static Mock<IChatCompletionModel> CreateMockModel(string response)
    {
        var mock = new Mock<IChatCompletionModel>();
        mock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        return mock;
    }
}

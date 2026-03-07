using FluentAssertions;
using Moq;
using Ouroboros.Agent.ConsolidatedMind;
using Ouroboros.Agent.NeuralSymbolic;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class ConsolidatedMindTests : IDisposable
{
    private readonly Mock<IChatCompletionModel> _primaryMock = new();
    private readonly Mock<IChatCompletionModel> _fallbackMock = new();
    private readonly Agent.ConsolidatedMind.ConsolidatedMind _sut;

    public ConsolidatedMindTests()
    {
        _sut = new Agent.ConsolidatedMind.ConsolidatedMind(new MindConfig
        {
            EnableThinking = false,
            EnableVerification = false,
            FallbackOnError = true,
        });
    }

    public void Dispose() => _sut.Dispose();

    // -- Registration --

    [Fact]
    public void RegisterSpecialist_AddsToCollection()
    {
        var specialist = CreateSpecialist(SpecializedRole.CodeExpert);
        _sut.RegisterSpecialist(specialist);

        _sut.Specialists.Should().ContainKey(SpecializedRole.CodeExpert);
    }

    [Fact]
    public void RegisterSpecialist_Null_Throws()
    {
        var act = () => _sut.RegisterSpecialist(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterSpecialists_AddsAll()
    {
        var specialists = new[]
        {
            CreateSpecialist(SpecializedRole.CodeExpert),
            CreateSpecialist(SpecializedRole.Creative),
        };
        _sut.RegisterSpecialists(specialists);

        _sut.Specialists.Should().HaveCount(2);
    }

    [Fact]
    public void RegisterSpecialist_DuplicateRole_Replaces()
    {
        var first = CreateSpecialist(SpecializedRole.CodeExpert, "Model1");
        var second = CreateSpecialist(SpecializedRole.CodeExpert, "Model2");

        _sut.RegisterSpecialist(first);
        _sut.RegisterSpecialist(second);

        _sut.Specialists.Should().HaveCount(1);
        _sut.Specialists[SpecializedRole.CodeExpert].ModelName.Should().Be("Model2");
    }

    // -- ProcessAsync --

    [Fact]
    public async Task ProcessAsync_NoSpecialists_Throws()
    {
        var act = () => _sut.ProcessAsync("hello");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No specialists*");
    }

    [Fact]
    public async Task ProcessAsync_RoutesToPrimarySpecialist()
    {
        _primaryMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("code response");

        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.CodeExpert, model: _primaryMock));

        var result = await _sut.ProcessAsync("Write a function to sort an array");

        result.Response.Should().Be("code response");
        result.UsedRoles.Should().Contain(SpecializedRole.CodeExpert);
    }

    [Fact]
    public async Task ProcessAsync_FallbackOnError_TriesFallbackSpecialist()
    {
        _primaryMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("primary failed"));
        _fallbackMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback response");

        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse, model: _primaryMock));
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.DeepReasoning, model: _fallbackMock));

        var result = await _sut.ProcessAsync("Hello");

        result.Response.Should().Be("fallback response");
        result.Confidence.Should().BeLessThan(1.0);
    }

    [Fact]
    public async Task ProcessAsync_AllFail_Throws()
    {
        _primaryMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail1"));
        _fallbackMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail2"));

        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse, model: _primaryMock));
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.DeepReasoning, model: _fallbackMock));

        var act = () => _sut.ProcessAsync("Hello");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Primary specialist failed*");
    }

    [Fact]
    public async Task ProcessAsync_Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _primaryMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse, model: _primaryMock));

        var act = () => _sut.ProcessAsync("Hello", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ProcessAsync_ReturnsExecutionTime()
    {
        _primaryMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("result");
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse, model: _primaryMock));

        var result = await _sut.ProcessAsync("Hello");

        result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ProcessAsync_FallsBackToFirstAvailableWhenRoleMissing()
    {
        _primaryMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("creative response");

        // Register only Creative, but prompt routes to CodeExpert
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.Creative, model: _primaryMock));

        var result = await _sut.ProcessAsync("Write a function to sort an array");

        result.Response.Should().Be("creative response");
    }

    // -- GenerateTextAsync (IChatCompletionModel) --

    [Fact]
    public async Task GenerateTextAsync_DelegatesAndReturnsString()
    {
        _primaryMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("text result");
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse, model: _primaryMock));

        var result = await _sut.GenerateTextAsync("Hello");

        result.Should().Be("text result");
    }

    // -- Metrics --

    [Fact]
    public async Task Metrics_UpdatedAfterSuccessfulExecution()
    {
        _primaryMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("result");
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse, model: _primaryMock, name: "TestModel"));

        await _sut.ProcessAsync("Hello");

        _sut.Metrics.Should().ContainKey("TestModel");
        _sut.Metrics["TestModel"].ExecutionCount.Should().Be(1);
        _sut.Metrics["TestModel"].SuccessRate.Should().Be(1.0);
    }

    // -- WithSymbolicFallback --

    [Fact]
    public void WithSymbolicFallback_Bridge_RegistersSymbolicReasoner()
    {
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        _sut.WithSymbolicFallback(bridgeMock.Object);

        _sut.Specialists.Should().ContainKey(SpecializedRole.SymbolicReasoner);
        _sut.Specialists[SpecializedRole.SymbolicReasoner].ModelName.Should().Contain("Symbolic");
    }

    [Fact]
    public void WithSymbolicFallback_Engine_RegistersSymbolicReasoner()
    {
        var engineMock = new Mock<IMeTTaEngine>();
        _sut.WithSymbolicFallback(engineMock.Object);

        _sut.Specialists.Should().ContainKey(SpecializedRole.SymbolicReasoner);
    }

    // -- ToStep / ToBranchStep --

    [Fact]
    public async Task ToStep_CreatesExecutableStep()
    {
        _primaryMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("step result");
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse, model: _primaryMock));

        var step = _sut.ToStep();
        var result = await step("test prompt");

        result.Response.Should().Be("step result");
    }

    // -- Dispose --

    [Fact]
    public void Dispose_DisposesDisposableModels()
    {
        var disposableMock = new Mock<IChatCompletionModel>();
        disposableMock.As<IDisposable>();
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse, model: disposableMock));

        _sut.Dispose();

        disposableMock.As<IDisposable>().Verify(d => d.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_ClearsSpecialists()
    {
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse));
        _sut.Dispose();

        _sut.Specialists.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        _sut.Dispose();
        var act = () => _sut.Dispose();
        act.Should().NotThrow();
    }

    // -- Helpers --

    private SpecializedModel CreateSpecialist(
        SpecializedRole role,
        string name = "TestModel",
        Mock<IChatCompletionModel>? model = null)
    {
        model ??= new Mock<IChatCompletionModel>();
        return new SpecializedModel(role, model.Object, name, new[] { "general" });
    }
}

using FluentAssertions;
using Moq;
using Ouroboros.Agent.ConsolidatedMind;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class SymbolicReasonerAdapterTests
{
    [Fact]
    public void Constructor_NullBridge_Throws()
    {
        var act = () => new SymbolicReasonerAdapter((INeuralSymbolicBridge)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullEngine_Throws()
    {
        var act = () => new SymbolicReasonerAdapter((IMeTTaEngine)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateTextAsync_WithBridge_ReturnsSymbolicResponse()
    {
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(),
                ReasoningMode.SymbolicOnly,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HybridReasoningResult, string>.Success(
                new HybridReasoningResult("symbolic answer", new List<ReasoningStep>(), 0.9, ReasoningMode.SymbolicOnly)));

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);
        var result = await adapter.GenerateTextAsync("What is 2+2?");

        result.Should().Contain("[Symbolic Reasoning]");
        result.Should().Contain("symbolic answer");
    }

    [Fact]
    public async Task GenerateTextAsync_WithEngine_ReturnsSymbolicResponse()
    {
        var engineMock = new Mock<IMeTTaEngine>();
        engineMock.Setup(e => e.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("engine answer"));

        var adapter = new SymbolicReasonerAdapter(engineMock.Object);
        var result = await adapter.GenerateTextAsync("What about logic?");

        result.Should().Contain("[Symbolic Reasoning]");
        result.Should().Contain("engine answer");
    }

    [Fact]
    public async Task GenerateTextAsync_BridgeFails_ReturnsLimitedResponse()
    {
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(),
                ReasoningMode.SymbolicOnly,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HybridReasoningResult, string>.Failure("reasoning failed"));

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);
        var result = await adapter.GenerateTextAsync("Something");

        result.Should().Contain("[Symbolic Reasoning - Limited Mode]");
    }

    [Fact]
    public async Task GenerateTextAsync_EngineFails_ReturnsLimitedResponse()
    {
        var engineMock = new Mock<IMeTTaEngine>();
        engineMock.Setup(e => e.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Failure("query failed"));

        var adapter = new SymbolicReasonerAdapter(engineMock.Object);
        var result = await adapter.GenerateTextAsync("Something");

        result.Should().Contain("[Symbolic Reasoning - Limited Mode]");
    }

    [Fact]
    public async Task GenerateTextAsync_ExceptionInBridge_ReturnsLimitedResponse()
    {
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(),
                It.IsAny<ReasoningMode>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bridge broken"));

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);
        var result = await adapter.GenerateTextAsync("test");

        result.Should().Contain("[Symbolic Reasoning - Limited Mode]");
        result.Should().Contain("bridge broken");
    }

    [Fact]
    public async Task GenerateTextAsync_NullPrompt_HandlesGracefully()
    {
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(),
                ReasoningMode.SymbolicOnly,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HybridReasoningResult, string>.Success(
                new HybridReasoningResult("answer", new List<ReasoningStep>(), 0.5, ReasoningMode.SymbolicOnly)));

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);
        var result = await adapter.GenerateTextAsync(null!);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateTextAsync_CancellationRequested_Throws()
    {
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(),
                It.IsAny<ReasoningMode>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);
        var act = () => adapter.GenerateTextAsync("test", new CancellationToken(true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateTextAsync_WithReasoningSteps_IncludesStepsInOutput()
    {
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        var steps = new List<ReasoningStep>
        {
            new() { StepNumber = 1, Description = "Step one", RuleApplied = "rule1" },
            new() { StepNumber = 2, Description = "Step two" },
        };
        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(),
                ReasoningMode.SymbolicOnly,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HybridReasoningResult, string>.Success(
                new HybridReasoningResult("answer", steps, 0.9, ReasoningMode.SymbolicOnly)));

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);
        var result = await adapter.GenerateTextAsync("test query");

        result.Should().Contain("Reasoning Steps:");
        result.Should().Contain("Step one");
        result.Should().Contain("Rule: rule1");
        result.Should().Contain("Step two");
    }

    [Fact]
    public async Task GenerateTextAsync_LongPrompt_TruncatesInLimitedMode()
    {
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(),
                It.IsAny<ReasoningMode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HybridReasoningResult, string>.Failure("fail"));

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);
        var longPrompt = new string('a', 300);
        var result = await adapter.GenerateTextAsync(longPrompt);

        result.Should().Contain("...");
    }
}

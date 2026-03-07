using FluentAssertions;
using Moq;
using Ouroboros.Agent.ConsolidatedMind;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class ConsolidatedMindProcessingTests : IDisposable
{
    private readonly Mock<IChatCompletionModel> _plannerMock = new();
    private readonly Mock<IChatCompletionModel> _coderMock = new();
    private readonly Mock<IChatCompletionModel> _synthesizerMock = new();
    private readonly Agent.ConsolidatedMind.ConsolidatedMind _sut;

    public ConsolidatedMindProcessingTests()
    {
        _sut = new Agent.ConsolidatedMind.ConsolidatedMind(new MindConfig
        {
            EnableThinking = false,
            EnableVerification = false,
            EnableParallelExecution = false,
            FallbackOnError = false,
        });
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task ProcessComplexAsync_NoPlannerRegistered_FallsBackToProcess()
    {
        var mockModel = new Mock<IChatCompletionModel>();
        mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("simple response");
        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse, mockModel.Object, "Quick", new[] { "general" }));

        var result = await _sut.ProcessComplexAsync("do something complex");

        result.Response.Should().Be("simple response");
    }

    [Fact]
    public async Task ProcessComplexAsync_PlannerReturnsEmpty_FallsBackToProcess()
    {
        _plannerMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(""); // empty plan
        var quickMock = new Mock<IChatCompletionModel>();
        quickMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("quick response");

        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.Planner, _plannerMock.Object, "Planner", new[] { "planning" }));
        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse, quickMock.Object, "Quick", new[] { "general" }));

        var result = await _sut.ProcessComplexAsync("do something");

        result.Response.Should().Be("quick response");
    }

    [Fact]
    public async Task ProcessComplexAsync_WithPlannerAndSubTasks_ExecutesSequentially()
    {
        _plannerMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("1. First step that is long enough to pass filter\n2. Second step that is long enough to pass filter");

        _coderMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("sub-result");

        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.Planner, _plannerMock.Object, "Planner", new[] { "planning" }));
        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse, _coderMock.Object, "Coder", new[] { "general" }));

        var result = await _sut.ProcessComplexAsync("build a sorting algorithm");

        result.Response.Should().Contain("sub-result");
        result.UsedRoles.Should().Contain(SpecializedRole.Planner);
    }

    [Fact]
    public async Task ProcessComplexAsync_WithSynthesizer_SynthesizesResults()
    {
        _plannerMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("1. First step that is long enough to pass filter\n2. Second step that is long enough to pass filter");
        _coderMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("sub-result");
        _synthesizerMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("synthesized response");

        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.Planner, _plannerMock.Object, "Planner", new[] { "planning" }));
        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse, _coderMock.Object, "Quick", new[] { "general" }));
        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.Synthesizer, _synthesizerMock.Object, "Synth", new[] { "synthesis" }));

        var result = await _sut.ProcessComplexAsync("build a complete feature");

        result.Response.Should().Be("synthesized response");
        result.UsedRoles.Should().Contain(SpecializedRole.Synthesizer);
        result.Confidence.Should().Be(0.8);
    }

    [Fact]
    public async Task ProcessComplexAsync_Parallel_ExecutesSubTasks()
    {
        using var parallelMind = new Agent.ConsolidatedMind.ConsolidatedMind(new MindConfig
        {
            EnableParallelExecution = true,
            MaxParallelism = 2,
            EnableThinking = false,
            EnableVerification = false,
            FallbackOnError = false,
        });

        _plannerMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("1. First step that is long enough\n2. Second step that is long enough");
        _coderMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("parallel result");

        parallelMind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.Planner, _plannerMock.Object, "Planner", new[] { "planning" }));
        parallelMind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse, _coderMock.Object, "Quick", new[] { "general" }));

        var result = await parallelMind.ProcessComplexAsync("multi-step task");

        result.Response.Should().Contain("parallel result");
    }

    [Fact]
    public async Task ProcessAsync_WithVerification_AddsVerifierRole()
    {
        using var mind = new Agent.ConsolidatedMind.ConsolidatedMind(new MindConfig
        {
            EnableVerification = true,
            EnableThinking = false,
            FallbackOnError = false,
        });

        var modelMock = new Mock<IChatCompletionModel>();
        modelMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("VALID: looks good");

        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse, modelMock.Object, "Quick", new[] { "general" }));
        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.Verifier, modelMock.Object, "Verifier", new[] { "verification" }));

        // Use a prompt that triggers RequiresVerification = true
        var result = await mind.ProcessAsync("prove that P = NP with formal mathematical verification");

        result.UsedRoles.Should().Contain(SpecializedRole.Verifier);
        result.WasVerified.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_WithThinking_UsesThinkingModel()
    {
        using var mind = new Agent.ConsolidatedMind.ConsolidatedMind(new MindConfig
        {
            EnableThinking = true,
            EnableVerification = false,
            FallbackOnError = false,
        });

        var thinkingMock = new Mock<IChatCompletionModel>();
        thinkingMock.As<Ouroboros.Providers.IThinkingChatModel>()
            .Setup(m => m.GenerateWithThinkingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ouroboros.Providers.ThinkingResponse("thinking steps", "final answer"));

        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.DeepReasoning, thinkingMock.Object, "Thinker", new[] { "reasoning", "logic", "analysis" }));

        var result = await mind.ProcessAsync("Why is the sky blue? Analyze and reason step by step");

        result.ThinkingContent.Should().Be("thinking steps");
        result.Response.Should().Be("final answer");
    }
}

// <copyright file="ConsolidatedMindTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.ConsolidatedMind;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class ConsolidatedMindTests : IDisposable
{
    private readonly Agent.ConsolidatedMind.ConsolidatedMind _sut;
    private readonly Mock<IChatCompletionModel> _mockModel;

    public ConsolidatedMindTests()
    {
        _mockModel = new Mock<IChatCompletionModel>();
        _sut = new Agent.ConsolidatedMind.ConsolidatedMind(MindConfig.Minimal());
    }

    public void Dispose() => _sut.Dispose();

    private SpecializedModel CreateSpecialist(
        SpecializedRole role,
        Mock<IChatCompletionModel>? model = null,
        string name = "test-model")
    {
        var m = model ?? _mockModel;
        return new SpecializedModel(role, m.Object, name, new[] { "general" });
    }

    // ── RegisterSpecialist ──────────────────────────────────────────────

    [Fact]
    public void RegisterSpecialist_NullSpecialist_ThrowsArgumentNull()
    {
        // Act
        var act = () => _sut.RegisterSpecialist(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterSpecialist_ValidSpecialist_AddsToSpecialists()
    {
        // Arrange
        var specialist = CreateSpecialist(SpecializedRole.CodeExpert);

        // Act
        _sut.RegisterSpecialist(specialist);

        // Assert
        _sut.Specialists.Should().ContainKey(SpecializedRole.CodeExpert);
        _sut.Specialists[SpecializedRole.CodeExpert].Should().BeSameAs(specialist);
    }

    [Fact]
    public void RegisterSpecialist_DuplicateRole_ReplacesExisting()
    {
        // Arrange
        var first = CreateSpecialist(SpecializedRole.CodeExpert, name: "first");
        var second = CreateSpecialist(SpecializedRole.CodeExpert, name: "second");

        // Act
        _sut.RegisterSpecialist(first);
        _sut.RegisterSpecialist(second);

        // Assert
        _sut.Specialists[SpecializedRole.CodeExpert].ModelName.Should().Be("second");
    }

    [Fact]
    public void RegisterSpecialist_InitializesMetrics()
    {
        // Arrange
        var specialist = CreateSpecialist(SpecializedRole.Creative, name: "creative-model");

        // Act
        _sut.RegisterSpecialist(specialist);

        // Assert
        _sut.Metrics.Should().ContainKey("creative-model");
        _sut.Metrics["creative-model"].ExecutionCount.Should().Be(0);
        _sut.Metrics["creative-model"].SuccessRate.Should().Be(1.0);
    }

    // ── RegisterSpecialists ─────────────────────────────────────────────

    [Fact]
    public void RegisterSpecialists_RegistersAll()
    {
        // Arrange
        var specialists = new[]
        {
            CreateSpecialist(SpecializedRole.CodeExpert, name: "code"),
            CreateSpecialist(SpecializedRole.DeepReasoning, name: "reasoning"),
            CreateSpecialist(SpecializedRole.Creative, name: "creative"),
        };

        // Act
        _sut.RegisterSpecialists(specialists);

        // Assert
        _sut.Specialists.Should().HaveCount(3);
        _sut.Specialists.Should().ContainKey(SpecializedRole.CodeExpert);
        _sut.Specialists.Should().ContainKey(SpecializedRole.DeepReasoning);
        _sut.Specialists.Should().ContainKey(SpecializedRole.Creative);
    }

    // ── ProcessAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WithNoSpecialists_ThrowsInvalidOperation()
    {
        // Act
        var act = async () => await _sut.ProcessAsync("hello");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No specialists registered*");
    }

    [Fact]
    public async Task ProcessAsync_WithMatchingSpecialist_UsesCorrectSpecialist()
    {
        // Arrange
        var codeMock = new Mock<IChatCompletionModel>();
        codeMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("code response");
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.CodeExpert, codeMock, "code-model"));

        // Act — "implement" triggers CodeExpert routing
        var result = await _sut.ProcessAsync("implement a function");

        // Assert
        result.Response.Should().Be("code response");
        result.UsedRoles.Should().Contain(SpecializedRole.CodeExpert);
    }

    [Fact]
    public async Task ProcessAsync_WithFallbackSpecialist_UsesFirstAvailable()
    {
        // Arrange — register only a Creative specialist, but send a code prompt
        var creativeMock = new Mock<IChatCompletionModel>();
        creativeMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("creative fallback");
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.Creative, creativeMock));

        // Act — "implement" would want CodeExpert, but only Creative is available
        var result = await _sut.ProcessAsync("implement a sorting function");

        // Assert
        result.Response.Should().Be("creative fallback");
    }

    [Fact]
    public async Task ProcessAsync_ReturnsExecutionTimeMs()
    {
        // Arrange
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("response");
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse));

        // Act
        var result = await _sut.ProcessAsync("hello");

        // Assert
        result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ProcessAsync_UpdatesMetrics_OnSuccess()
    {
        // Arrange
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok");
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse, name: "quick"));

        // Act
        await _sut.ProcessAsync("hello");

        // Assert
        _sut.Metrics["quick"].ExecutionCount.Should().Be(1);
        _sut.Metrics["quick"].SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public async Task ProcessAsync_WithFallbackOnError_FallsBackOnFailure()
    {
        // Arrange — config with fallback enabled
        using var mind = new Agent.ConsolidatedMind.ConsolidatedMind(
            new MindConfig(FallbackOnError: true, EnableVerification: false, EnableThinking: false));

        var primaryMock = new Mock<IChatCompletionModel>();
        primaryMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("primary failed"));

        var fallbackMock = new Mock<IChatCompletionModel>();
        fallbackMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback response");

        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.CodeExpert, primaryMock.Object, "primary", new[] { "code" }));
        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.DeepReasoning, fallbackMock.Object, "fallback", new[] { "reasoning" }));

        // Act — triggers CodeExpert but it fails, should fallback to DeepReasoning
        var result = await mind.ProcessAsync("implement a function");

        // Assert
        result.Response.Should().Be("fallback response");
        result.Confidence.Should().BeLessThan(1.0); // Reduced confidence for fallback
    }

    [Fact]
    public async Task ProcessAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse));

        // Act
        var act = async () => await _sut.ProcessAsync("hello", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── GenerateTextAsync (IChatCompletionModel) ────────────────────────

    [Fact]
    public async Task GenerateTextAsync_DelegatesToProcessAsync()
    {
        // Arrange
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("text response");
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse));

        // Act
        var result = await _sut.GenerateTextAsync("hello");

        // Assert
        result.Should().Be("text response");
    }

    // ── WithSymbolicFallback ────────────────────────────────────────────

    [Fact]
    public void WithSymbolicFallback_WithBridge_RegistersSymbolicReasoner()
    {
        // Arrange
        var bridge = new Mock<INeuralSymbolicBridge>();

        // Act
        var returned = _sut.WithSymbolicFallback(bridge.Object);

        // Assert
        returned.Should().BeSameAs(_sut); // fluent chaining
        _sut.Specialists.Should().ContainKey(SpecializedRole.SymbolicReasoner);
        _sut.Specialists[SpecializedRole.SymbolicReasoner].ModelName.Should().Contain("Symbolic Reasoner");
    }

    [Fact]
    public void WithSymbolicFallback_WithEngine_RegistersSymbolicReasoner()
    {
        // Arrange
        var engine = new Mock<IMeTTaEngine>();

        // Act
        var returned = _sut.WithSymbolicFallback(engine.Object);

        // Assert
        returned.Should().BeSameAs(_sut);
        _sut.Specialists.Should().ContainKey(SpecializedRole.SymbolicReasoner);
        _sut.Specialists[SpecializedRole.SymbolicReasoner].ModelName.Should().Contain("MeTTa");
    }

    // ── ToStep ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ToStep_ReturnsWorkingStep()
    {
        // Arrange
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("step result");
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse));

        // Act
        var step = _sut.ToStep();
        var result = await step("hello");

        // Assert
        result.Response.Should().Be("step result");
    }

    // ── Dispose ─────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DisposesDisposableModels()
    {
        // Arrange
        var disposableMock = new Mock<IChatCompletionModel>();
        var disposable = disposableMock.As<IDisposable>();
        using var mind = new Agent.ConsolidatedMind.ConsolidatedMind();
        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse, disposableMock.Object, "model", new[] { "test" }));

        // Act
        mind.Dispose();

        // Assert
        disposable.Verify(d => d.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_ClearsSpecialists()
    {
        // Arrange
        using var mind = new Agent.ConsolidatedMind.ConsolidatedMind();
        mind.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse));

        // Act
        mind.Dispose();

        // Assert
        mind.Specialists.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        // Arrange
        var disposableMock = new Mock<IChatCompletionModel>();
        var disposable = disposableMock.As<IDisposable>();
        var mind = new Agent.ConsolidatedMind.ConsolidatedMind();
        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse, disposableMock.Object, "model", new[] { "test" }));

        // Act
        mind.Dispose();
        mind.Dispose();

        // Assert
        disposable.Verify(d => d.Dispose(), Times.Once);
    }

    // ── ProcessComplexAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ProcessComplexAsync_WithoutPlanner_DelegatesToProcessAsync()
    {
        // Arrange
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("simple response");
        _sut.RegisterSpecialist(CreateSpecialist(SpecializedRole.QuickResponse));

        // Act
        var result = await _sut.ProcessComplexAsync("complex task");

        // Assert
        result.Response.Should().Be("simple response");
    }

    [Fact]
    public async Task ProcessComplexAsync_WithPlanner_DecomposesTask()
    {
        // Arrange
        var plannerMock = new Mock<IChatCompletionModel>();
        plannerMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("1. Analyze the requirements\n2. Design the solution");

        var workerMock = new Mock<IChatCompletionModel>();
        workerMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("sub-task result");

        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.Planner, plannerMock.Object, "planner", new[] { "planning" }));
        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse, workerMock.Object, "worker", new[] { "general" }));

        // Act
        var result = await _sut.ProcessComplexAsync("Build a complex system");

        // Assert
        result.UsedRoles.Should().Contain(SpecializedRole.Planner);
        result.Response.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessComplexAsync_WithSynthesizer_SynthesizesResults()
    {
        // Arrange
        var plannerMock = new Mock<IChatCompletionModel>();
        plannerMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("1. First sub-task here\n2. Second sub-task here");

        var workerMock = new Mock<IChatCompletionModel>();
        workerMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("worker result");

        var synthMock = new Mock<IChatCompletionModel>();
        synthMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("synthesized response");

        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.Planner, plannerMock.Object, "planner", new[] { "planning" }));
        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse, workerMock.Object, "worker", new[] { "general" }));
        _sut.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.Synthesizer, synthMock.Object, "synth", new[] { "summarization" }));

        // Act
        var result = await _sut.ProcessComplexAsync("complex task");

        // Assert
        result.Response.Should().Be("synthesized response");
        result.UsedRoles.Should().Contain(SpecializedRole.Synthesizer);
        result.Confidence.Should().Be(0.8);
    }
}

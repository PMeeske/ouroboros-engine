// <copyright file="SymbolicReasonerAdapterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Ouroboros.Agent.ConsolidatedMind;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class SymbolicReasonerAdapterTests
{
    // ── Constructor guards ──────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullBridge_ThrowsArgumentNull()
    {
        // Act
        var act = () => new SymbolicReasonerAdapter((INeuralSymbolicBridge)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullEngine_ThrowsArgumentNull()
    {
        // Act
        var act = () => new SymbolicReasonerAdapter((IMeTTaEngine)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Bridge-based reasoning ──────────────────────────────────────────

    [Fact]
    public async Task GenerateTextAsync_WithBridge_Success_ReturnsFormattedResponse()
    {
        // Arrange
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        var steps = new List<ReasoningStep>
        {
            new(1, "Applied modus ponens", "rule1", ReasoningStepType.SymbolicDeduction),
            new(2, "Derived conclusion", "rule2", ReasoningStepType.SymbolicDeduction),
        };
        var reasoningResult = new ReasoningResult(
            "test query", "symbolic answer", ReasoningMode.SymbolicOnly, steps, 0.9,
            SymbolicSucceeded: true, NeuralSucceeded: false, Duration: TimeSpan.FromMilliseconds(100));

        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(), ReasoningMode.SymbolicOnly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReasoningResult, string>.Success(reasoningResult));

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);

        // Act
        var result = await adapter.GenerateTextAsync("What is logic?");

        // Assert
        result.Should().Contain("[Symbolic Reasoning]");
        result.Should().Contain("symbolic answer");
        result.Should().Contain("Reasoning Steps:");
        result.Should().Contain("Applied modus ponens");
        result.Should().Contain("Rule: rule1");
    }

    [Fact]
    public async Task GenerateTextAsync_WithBridge_Failure_ReturnsLimitedResponse()
    {
        // Arrange
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(), ReasoningMode.SymbolicOnly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReasoningResult, string>.Failure("bridge error"));

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);

        // Act
        var result = await adapter.GenerateTextAsync("test prompt");

        // Assert
        result.Should().Contain("[Symbolic Reasoning - Limited Mode]");
        result.Should().Contain("test prompt");
    }

    // ── Engine-based reasoning ──────────────────────────────────────────

    [Fact]
    public async Task GenerateTextAsync_WithEngine_Success_ReturnsFormattedResponse()
    {
        // Arrange
        var engineMock = new Mock<IMeTTaEngine>();
        engineMock.Setup(e => e.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("engine result"));

        var adapter = new SymbolicReasonerAdapter(engineMock.Object);

        // Act
        var result = await adapter.GenerateTextAsync("What is the meaning of consciousness?");

        // Assert
        result.Should().Contain("[Symbolic Reasoning]");
        result.Should().Contain("engine result");
    }

    [Fact]
    public async Task GenerateTextAsync_WithEngine_Failure_ReturnsLimitedResponse()
    {
        // Arrange
        var engineMock = new Mock<IMeTTaEngine>();
        engineMock.Setup(e => e.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Failure("query failed"));

        var adapter = new SymbolicReasonerAdapter(engineMock.Object);

        // Act
        var result = await adapter.GenerateTextAsync("test");

        // Assert
        result.Should().Contain("[Symbolic Reasoning - Limited Mode]");
    }

    [Fact]
    public async Task GenerateTextAsync_WithEngine_ExtractsQueryFromPrompt()
    {
        // Arrange
        var engineMock = new Mock<IMeTTaEngine>();
        engineMock.Setup(e => e.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("result"));

        var adapter = new SymbolicReasonerAdapter(engineMock.Object);

        // Act
        await adapter.GenerateTextAsync("What is the meaning of consciousness and awareness?");

        // Assert — verify the query was constructed from key terms
        engineMock.Verify(e => e.ExecuteQueryAsync(
            It.Is<string>(q => q.Contains("(query") && q.Contains("(concept")),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GenerateTextAsync_WithEngine_ShortPrompt_UsesFallbackQuery()
    {
        // Arrange
        var engineMock = new Mock<IMeTTaEngine>();
        engineMock.Setup(e => e.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("result"));

        var adapter = new SymbolicReasonerAdapter(engineMock.Object);

        // Act — very short prompt with only short words after question word removal
        await adapter.GenerateTextAsync("Hi");

        // Assert — falls back to generic query
        engineMock.Verify(e => e.ExecuteQueryAsync(
            It.Is<string>(q => q.Contains("(query (concept general))")),
            It.IsAny<CancellationToken>()));
    }

    // ── Error handling ──────────────────────────────────────────────────

    [Fact]
    public async Task GenerateTextAsync_OnException_ReturnsLimitedResponse()
    {
        // Arrange
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(), It.IsAny<ReasoningMode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected error"));

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);

        // Act — should NOT throw
        var result = await adapter.GenerateTextAsync("test prompt");

        // Assert
        result.Should().Contain("[Symbolic Reasoning - Limited Mode]");
        result.Should().Contain("unexpected error");
    }

    [Fact]
    public async Task GenerateTextAsync_OnCancellation_PropagatesCancellation()
    {
        // Arrange
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(), It.IsAny<ReasoningMode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);

        // Act
        var act = async () => await adapter.GenerateTextAsync("test", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateTextAsync_WithNullPrompt_HandlesGracefully()
    {
        // Arrange
        var engineMock = new Mock<IMeTTaEngine>();
        engineMock.Setup(e => e.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("result"));

        var adapter = new SymbolicReasonerAdapter(engineMock.Object);

        // Act — null prompt should not throw
        var result = await adapter.GenerateTextAsync(null!);

        // Assert
        result.Should().NotBeNull();
    }

    // ── Response formatting ─────────────────────────────────────────────

    [Fact]
    public async Task GenerateTextAsync_WithStepsWithoutRules_FormatsCorrectly()
    {
        // Arrange
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        var steps = new List<ReasoningStep>
        {
            new(1, "Initial analysis", string.Empty, ReasoningStepType.NeuralInference),
        };
        var reasoningResult = new ReasoningResult(
            "query", "answer", ReasoningMode.SymbolicOnly, steps, 0.8,
            SymbolicSucceeded: true, NeuralSucceeded: false, Duration: TimeSpan.FromMilliseconds(50));

        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(), ReasoningMode.SymbolicOnly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReasoningResult, string>.Success(reasoningResult));

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);

        // Act
        var result = await adapter.GenerateTextAsync("test");

        // Assert
        result.Should().Contain("1. Initial analysis");
    }

    [Fact]
    public async Task GenerateTextAsync_LongPrompt_TruncatesInLimitedResponse()
    {
        // Arrange
        var bridgeMock = new Mock<INeuralSymbolicBridge>();
        bridgeMock.Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(), It.IsAny<ReasoningMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReasoningResult, string>.Failure("error"));

        var adapter = new SymbolicReasonerAdapter(bridgeMock.Object);
        var longPrompt = new string('x', 300);

        // Act
        var result = await adapter.GenerateTextAsync(longPrompt);

        // Assert — prompt should be truncated with "..."
        result.Should().Contain("...");
    }
}

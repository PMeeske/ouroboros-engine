// <copyright file="ResilientReasonerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;
using Ouroboros.Agent.Resilience;
using Ouroboros.Core.Resilience;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;
using NSReasoningMode = Ouroboros.Agent.NeuralSymbolic.ReasoningMode;
using NSReasoningStep = Ouroboros.Agent.NeuralSymbolic.ReasoningStep;

namespace Ouroboros.Tests.Resilience;

[Trait("Category", "Unit")]
public class ResilientReasonerTests
{
    private readonly Mock<INeuralSymbolicBridge> _bridgeMock = new();
    private readonly Mock<IChatCompletionModel> _llmMock = new();

    [Fact]
    public void Constructor_NullBridge_Throws()
    {
        var act = () => new ResilientReasoner(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidBridge_DoesNotThrow()
    {
        var act = () => new ResilientReasoner(_bridgeMock.Object);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithAllParams_DoesNotThrow()
    {
        var config = new CircuitBreakerConfig();
        var logger = new Mock<ILogger<ResilientReasoner>>().Object;
        var act = () => new ResilientReasoner(_bridgeMock.Object, _llmMock.Object, config, logger);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ReasonAsync_EmptyQuery_ReturnsFailure()
    {
        var reasoner = new ResilientReasoner(_bridgeMock.Object);
        var result = await reasoner.ReasonAsync("");
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task ReasonAsync_WhitespaceQuery_ReturnsFailure()
    {
        var reasoner = new ResilientReasoner(_bridgeMock.Object);
        var result = await reasoner.ReasonAsync("   ");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ReasonAsync_ValidQuery_DelegatesToBridge()
    {
        var expectedResult = new ReasoningResult(
            "What is 2+2?", "answer", NSReasoningMode.Parallel,
            new List<NSReasoningStep>(), 0.9, true, true, TimeSpan.FromMilliseconds(100));
        _bridgeMock
            .Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(),
                It.IsAny<NSReasoningMode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReasoningResult, string>.Success(expectedResult));

        var reasoner = new ResilientReasoner(_bridgeMock.Object);
        var result = await reasoner.ReasonAsync("What is 2+2?");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("answer");
    }

    [Fact]
    public void GetHealth_ReturnsValidHealth()
    {
        var reasoner = new ResilientReasoner(_bridgeMock.Object);
        var health = reasoner.GetHealth();

        health.Should().NotBeNull();
        health.SymbolicAvailable.Should().BeTrue();
        health.ConsecutiveLlmFailures.Should().Be(0);
    }

    [Fact]
    public void GetHealth_CircuitState_IsClosedInitially()
    {
        var reasoner = new ResilientReasoner(_bridgeMock.Object);
        var health = reasoner.GetHealth();

        health.CircuitState.Should().Be("Closed");
    }

    [Fact]
    public async Task ReasonAsync_SymbolicOnlyMode_BypassesCircuitBreaker()
    {
        var expectedResult = new ReasoningResult(
            "query", "symbolic answer", NSReasoningMode.SymbolicOnly,
            new List<NSReasoningStep>(), 0.8, true, false, TimeSpan.FromMilliseconds(50));
        _bridgeMock
            .Setup(b => b.HybridReasonAsync(
                It.IsAny<string>(),
                It.IsAny<NSReasoningMode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReasoningResult, string>.Success(expectedResult));

        var reasoner = new ResilientReasoner(_bridgeMock.Object);
        var result = await reasoner.ReasonAsync("query", Core.Resilience.ReasoningMode.SymbolicOnly);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that Core.Resilience.ReasoningMode and NeuralSymbolic.ReasoningMode
    /// have identical integer values, which is required for the cast in ReasonWithMode.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ReasoningMode_EnumValues_AreAligned(int value)
    {
        var coreMode = (Core.Resilience.ReasoningMode)value;
        var nsMode = (Ouroboros.Agent.NeuralSymbolic.ReasoningMode)value;

        coreMode.ToString().Should().Be(nsMode.ToString());
    }
}

// <copyright file="SimpleTerminalPredictorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;

namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

/// <summary>
/// Unit tests for the SimpleTerminalPredictor class.
/// </summary>
[Trait("Category", "Unit")]
public class SimpleTerminalPredictorTests
{
    // --- PredictAsync ---

    [Fact]
    public async Task PredictAsync_LargeNegativeBias_PredictsNonTerminal()
    {
        // Arrange - large negative bias makes sigmoid output near 0
        var weights = new float[] { 0f, 0f };
        var sut = new SimpleTerminalPredictor(weights, -10f);
        var state = CreateState(new float[] { 0f, 0f });

        // Act
        var result = await sut.PredictAsync(state);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task PredictAsync_LargePositiveBias_PredictsTerminal()
    {
        // Arrange - large positive bias makes sigmoid output near 1
        var weights = new float[] { 0f, 0f };
        var sut = new SimpleTerminalPredictor(weights, 10f);
        var state = CreateState(new float[] { 0f, 0f });

        // Act
        var result = await sut.PredictAsync(state);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PredictAsync_ZeroBias_AtThreshold()
    {
        // Arrange - sigmoid(0) = 0.5 = default threshold
        var weights = new float[] { 0f, 0f };
        var sut = new SimpleTerminalPredictor(weights, 0f, threshold: 0.5f);
        var state = CreateState(new float[] { 0f, 0f });

        // Act
        var result = await sut.PredictAsync(state);

        // Assert
        result.Should().BeTrue(); // sigmoid(0) = 0.5 >= 0.5
    }

    [Fact]
    public async Task PredictAsync_CustomThreshold_RespectedCorrectly()
    {
        // Arrange - sigmoid(0) = 0.5, threshold 0.6 -> not terminal
        var weights = new float[] { 0f };
        var sut = new SimpleTerminalPredictor(weights, 0f, threshold: 0.6f);
        var state = CreateState(new float[] { 0f });

        // Act
        var result = await sut.PredictAsync(state);

        // Assert
        result.Should().BeFalse(); // 0.5 < 0.6
    }

    [Fact]
    public async Task PredictAsync_WeightsShorterThanEmbedding_HandlesGracefully()
    {
        // Arrange
        var weights = new float[] { 1f };
        var sut = new SimpleTerminalPredictor(weights, 0f);
        var state = CreateState(new float[] { 1f, 2f, 3f });

        // Act
        var act = () => sut.PredictAsync(state);

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PredictAsync_WithPositiveWeightsAndPositiveEmbedding_TerminalLikely()
    {
        // Arrange - positive logit -> sigmoid > 0.5
        var weights = new float[] { 5f, 5f };
        var sut = new SimpleTerminalPredictor(weights, 0f);
        var state = CreateState(new float[] { 1f, 1f });

        // Act
        var result = await sut.PredictAsync(state);

        // Assert
        result.Should().BeTrue();
    }

    // --- CreateRandom ---

    [Fact]
    public void CreateRandom_WithFeatureSize_ReturnsPredictor()
    {
        // Act
        var sut = SimpleTerminalPredictor.CreateRandom(10);

        // Assert
        sut.Should().NotBeNull();
    }

    [Fact]
    public void CreateRandom_SameSeed_ProducesDeterministic()
    {
        // Act
        var sut1 = SimpleTerminalPredictor.CreateRandom(5, seed: 42);
        var sut2 = SimpleTerminalPredictor.CreateRandom(5, seed: 42);

        // Verify same predictions
        var state = CreateState(new float[] { 1f, 2f, 3f, 4f, 5f });
        var result1 = sut1.PredictAsync(state).Result;
        var result2 = sut2.PredictAsync(state).Result;

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public async Task CreateRandom_DefaultSeed_BiasedTowardsNonTerminal()
    {
        // Arrange - CreateRandom uses bias of -2.0f
        var sut = SimpleTerminalPredictor.CreateRandom(5);
        var state = CreateState(new float[] { 0f, 0f, 0f, 0f, 0f });

        // Act - with zero state, only bias contributes: sigmoid(-2) ~ 0.12
        var result = await sut.PredictAsync(state);

        // Assert
        result.Should().BeFalse();
    }

    private static State CreateState(float[] embedding)
    {
        return new State(new Dictionary<string, object>(), embedding);
    }
}

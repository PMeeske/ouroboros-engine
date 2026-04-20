// <copyright file="SimpleRewardPredictorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;
using Action = Ouroboros.Agent.MetaAI.WorldModel.Action;

namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

/// <summary>
/// Unit tests for the SimpleRewardPredictor class.
/// </summary>
[Trait("Category", "Unit")]
public class SimpleRewardPredictorTests
{
    // --- PredictAsync ---

    [Fact]
    public async Task PredictAsync_WithZeroWeightsAndZeroBias_ReturnsZero()
    {
        // Arrange
        var weights = new float[] { 0f, 0f, 0f, 0f, 0f };
        var sut = new SimpleRewardPredictor(weights, 0f);
        var current = CreateState(new float[] { 1f, 2f });
        var action = new Action("test", new Dictionary<string, object>());
        var next = CreateState(new float[] { 3f, 4f });

        // Act
        var result = await sut.PredictAsync(current, action, next);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public async Task PredictAsync_WithBiasOnly_ReturnsBias()
    {
        // Arrange
        var weights = new float[] { 0f, 0f, 0f };
        var sut = new SimpleRewardPredictor(weights, 2.5f);
        var current = CreateState(new float[] { 0f });
        var action = new Action("test", new Dictionary<string, object>());
        var next = CreateState(new float[] { 0f });

        // Act
        var result = await sut.PredictAsync(current, action, next);

        // Assert
        result.Should().BeApproximately(2.5, 0.01);
    }

    [Fact]
    public async Task PredictAsync_WithWeights_ComputesLinearPrediction()
    {
        // Arrange
        var weights = new float[] { 1f, 1f, 1f, 1f, 1f };
        var sut = new SimpleRewardPredictor(weights, 0f);
        var current = CreateState(new float[] { 1f, 1f });
        var action = new Action("test", new Dictionary<string, object>());
        var next = CreateState(new float[] { 1f, 1f });

        // Act
        var result = await sut.PredictAsync(current, action, next);

        // Assert
        result.Should().NotBe(0.0);
    }

    [Fact]
    public async Task PredictAsync_WeightsShorterThanFeatures_DoesNotThrow()
    {
        // Arrange
        var weights = new float[] { 1f };
        var sut = new SimpleRewardPredictor(weights, 0f);
        var current = CreateState(new float[] { 1f, 2f, 3f });
        var action = new Action("test", new Dictionary<string, object>());
        var next = CreateState(new float[] { 4f, 5f, 6f });

        // Act
        var result = await sut.PredictAsync(current, action, next);

        // Assert
        result.Should().BeOfType(typeof(double));
    }

    // --- CreateRandom ---

    [Fact]
    public void CreateRandom_WithFeatureSize_ReturnsPredictor()
    {
        // Act
        var sut = SimpleRewardPredictor.CreateRandom(10);

        // Assert
        sut.Should().NotBeNull();
    }

    [Fact]
    public void CreateRandom_SameSeed_ProducesSamePredictor()
    {
        // Act
        var sut1 = SimpleRewardPredictor.CreateRandom(5, seed: 123);
        var sut2 = SimpleRewardPredictor.CreateRandom(5, seed: 123);

        // Verify they produce the same predictions
        var state = CreateState(new float[] { 1f, 2f });
        var action = new Action("test", new Dictionary<string, object>());

        var result1 = sut1.PredictAsync(state, action, state).Result;
        var result2 = sut2.PredictAsync(state, action, state).Result;

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public async Task CreateRandom_DifferentSeeds_ProduceDifferentPredictions()
    {
        // Arrange
        var sut1 = SimpleRewardPredictor.CreateRandom(10, seed: 1);
        var sut2 = SimpleRewardPredictor.CreateRandom(10, seed: 999);
        var state = CreateState(new float[] { 1f, 2f, 3f, 4f });
        var action = new Action("test", new Dictionary<string, object>());

        // Act
        var result1 = await sut1.PredictAsync(state, action, state);
        var result2 = await sut2.PredictAsync(state, action, state);

        // Assert
        result1.Should().NotBe(result2);
    }

    private static State CreateState(float[] embedding)
    {
        return new State(new Dictionary<string, object>(), embedding);
    }
}

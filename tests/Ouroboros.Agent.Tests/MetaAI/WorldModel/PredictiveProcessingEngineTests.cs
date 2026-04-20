// <copyright file="PredictiveProcessingEngineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;
using static Ouroboros.Agent.MetaAI.WorldModel.PredictiveProcessingEngine;

namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

/// <summary>
/// Unit tests for the PredictiveProcessingEngine implementing Friston's Free Energy Principle.
/// </summary>
[Trait("Category", "Unit")]
public class PredictiveProcessingEngineTests
{
    private readonly PredictiveProcessingEngine _sut;

    public PredictiveProcessingEngineTests()
    {
        _sut = new PredictiveProcessingEngine(freeEnergyThreshold: 0.6);
    }

    // --- GeneratePrediction ---

    [Fact]
    public void GeneratePrediction_SensoryLevel_HighPrecision()
    {
        // Act
        var prediction = _sut.GeneratePrediction("temperature is 72F", PredictionLevel.Sensory);

        // Assert
        prediction.Should().NotBeNull();
        prediction.Content.Should().Be("temperature is 72F");
        prediction.Level.Should().Be(PredictionLevel.Sensory);
        prediction.Precision.Should().Be(0.8);
        prediction.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GeneratePrediction_SemanticLevel_MediumPrecision()
    {
        // Act
        var prediction = _sut.GeneratePrediction("user wants help", PredictionLevel.Semantic);

        // Assert
        prediction.Precision.Should().Be(0.6);
        prediction.Level.Should().Be(PredictionLevel.Semantic);
    }

    [Fact]
    public void GeneratePrediction_StrategicLevel_LowPrecision()
    {
        // Act
        var prediction = _sut.GeneratePrediction("project will succeed", PredictionLevel.Strategic);

        // Assert
        prediction.Precision.Should().Be(0.4);
        prediction.Level.Should().Be(PredictionLevel.Strategic);
    }

    [Fact]
    public void GeneratePrediction_NullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.GeneratePrediction(null!, PredictionLevel.Sensory);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // --- ComputeError ---

    [Fact]
    public void ComputeError_PerfectMatch_ZeroMagnitude()
    {
        // Arrange
        var prediction = _sut.GeneratePrediction("the cat sat on the mat", PredictionLevel.Sensory);

        // Act
        var error = _sut.ComputeError(prediction.Id, "the cat sat on the mat");

        // Assert
        error.Magnitude.Should().Be(0.0);
        error.Original.Should().Be(prediction);
    }

    [Fact]
    public void ComputeError_NoOverlap_HighMagnitude()
    {
        // Arrange
        var prediction = _sut.GeneratePrediction("alpha beta gamma", PredictionLevel.Sensory);

        // Act
        var error = _sut.ComputeError(prediction.Id, "delta epsilon zeta");

        // Assert
        error.Magnitude.Should().Be(1.0);
    }

    [Fact]
    public void ComputeError_PartialOverlap_IntermediateMagnitude()
    {
        // Arrange
        var prediction = _sut.GeneratePrediction("the red fox jumped", PredictionLevel.Sensory);

        // Act
        var error = _sut.ComputeError(prediction.Id, "the blue fox ran");

        // Assert
        error.Magnitude.Should().BeGreaterThan(0.0);
        error.Magnitude.Should().BeLessThan(1.0);
    }

    [Fact]
    public void ComputeError_UnknownPredictionId_ReturnsDefaultError()
    {
        // Act
        var error = _sut.ComputeError("nonexistent-id", "some observation");

        // Assert
        error.Magnitude.Should().Be(1.0);
        error.Original.Id.Should().BeEmpty();
    }

    [Fact]
    public void ComputeError_NullPredictionId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.ComputeError(null!, "observation");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComputeError_NullObservation_ThrowsArgumentNullException()
    {
        // Arrange
        var prediction = _sut.GeneratePrediction("test", PredictionLevel.Sensory);

        // Act
        var act = () => _sut.ComputeError(prediction.Id, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComputeError_PrecisionWeightMatchesPredictionPrecision()
    {
        // Arrange
        var prediction = _sut.GeneratePrediction("test context", PredictionLevel.Strategic);

        // Act
        var error = _sut.ComputeError(prediction.Id, "different context");

        // Assert
        error.PrecisionWeight.Should().Be(0.4); // Strategic precision
    }

    // --- ComputeFreeEnergy ---

    [Fact]
    public void ComputeFreeEnergy_NoErrors_ZeroEnergy()
    {
        // Act
        var state = _sut.ComputeFreeEnergy();

        // Assert
        state.TotalFreeEnergy.Should().Be(0.0);
        state.ActiveErrors.Should().BeEmpty();
        state.RecommendedAction.Should().Be("update-beliefs");
    }

    [Fact]
    public void ComputeFreeEnergy_LowErrors_RecommendsBeliefUpdate()
    {
        // Arrange
        var prediction = _sut.GeneratePrediction("the weather is sunny", PredictionLevel.Sensory);
        _sut.ComputeError(prediction.Id, "the weather is partly sunny");

        // Act
        var state = _sut.ComputeFreeEnergy();

        // Assert
        state.RecommendedAction.Should().Be("update-beliefs");
    }

    [Fact]
    public void ComputeFreeEnergy_HighErrors_RecommendsActiveInference()
    {
        // Arrange - create many large errors to exceed threshold
        for (int i = 0; i < 5; i++)
        {
            var prediction = _sut.GeneratePrediction($"alpha beta {i}", PredictionLevel.Sensory);
            _sut.ComputeError(prediction.Id, $"gamma delta {i}");
        }

        // Act
        var state = _sut.ComputeFreeEnergy();

        // Assert
        state.TotalFreeEnergy.Should().BeGreaterThan(0.6);
        state.RecommendedAction.Should().Be("act-to-reduce-surprise");
    }

    // --- ComputeBeliefUpdateMagnitude ---

    [Fact]
    public void ComputeBeliefUpdateMagnitude_HighPrecision_SmallUpdate()
    {
        // Arrange
        var prediction = new Prediction("id", "content", PredictionLevel.Sensory, 0.8, DateTime.UtcNow);
        var error = new PredictionError(prediction, "observation", 0.5, 0.8);

        // Act
        var magnitude = PredictiveProcessingEngine.ComputeBeliefUpdateMagnitude(error);

        // Assert
        magnitude.Should().BeApproximately(0.1, 0.001); // 0.5 * (1 - 0.8)
    }

    [Fact]
    public void ComputeBeliefUpdateMagnitude_LowPrecision_LargeUpdate()
    {
        // Arrange
        var prediction = new Prediction("id", "content", PredictionLevel.Strategic, 0.2, DateTime.UtcNow);
        var error = new PredictionError(prediction, "observation", 0.8, 0.2);

        // Act
        var magnitude = PredictiveProcessingEngine.ComputeBeliefUpdateMagnitude(error);

        // Assert
        magnitude.Should().BeApproximately(0.64, 0.001); // 0.8 * (1 - 0.2)
    }

    [Fact]
    public void ComputeBeliefUpdateMagnitude_NullError_ThrowsArgumentNullException()
    {
        // Act
        var act = () => PredictiveProcessingEngine.ComputeBeliefUpdateMagnitude(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // --- EstimateContextPrecision ---

    [Fact]
    public void EstimateContextPrecision_EmptyContext_ReturnsMinPrecision()
    {
        // Act
        var precision = _sut.EstimateContextPrecision("");

        // Assert
        precision.Should().Be(0.3);
    }

    [Fact]
    public void EstimateContextPrecision_NullContext_ReturnsMinPrecision()
    {
        // Act
        var precision = _sut.EstimateContextPrecision(null!);

        // Assert
        precision.Should().Be(0.3);
    }

    [Fact]
    public void EstimateContextPrecision_NoMatchingPredictions_ReturnsBasePrecision()
    {
        // Act
        var precision = _sut.EstimateContextPrecision("novel concept");

        // Assert
        precision.Should().Be(0.3);
    }

    [Fact]
    public void EstimateContextPrecision_MatchingPredictions_IncreasesPrecision()
    {
        // Arrange
        _sut.GeneratePrediction("weather forecast sunny", PredictionLevel.Sensory);
        _sut.GeneratePrediction("weather report for today", PredictionLevel.Semantic);

        // Act
        var precision = _sut.EstimateContextPrecision("weather");

        // Assert
        precision.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public void EstimateContextPrecision_ManyMatches_CapsAt095()
    {
        // Arrange
        for (int i = 0; i < 20; i++)
        {
            _sut.GeneratePrediction($"weather data point {i}", PredictionLevel.Sensory);
        }

        // Act
        var precision = _sut.EstimateContextPrecision("weather");

        // Assert
        precision.Should().BeLessThanOrEqualTo(0.95);
    }
}

using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Tests.Metacognition;

[Trait("Category", "Unit")]
public sealed class BayesianSelfAssessorTests
{
    [Fact]
    public void Constructor_InitializesAllDimensionScoresWithUnknown()
    {
        // Act
        var assessor = new BayesianSelfAssessor();

        // Assert - assess each dimension and check it returns unknown-like values
        foreach (var dimension in Enum.GetValues<PerformanceDimension>())
        {
            var result = assessor.AssessDimensionAsync(dimension).Result;
            result.IsSuccess.Should().BeTrue();
            result.Value.Score.Should().Be(0.5);
            result.Value.Confidence.Should().Be(0.0);
        }
    }

    [Fact]
    public void Constructor_WithInitialBeliefs_SetsBeliefs()
    {
        // Arrange
        var beliefs = new[] { CapabilityBelief.Create("coding", 0.8, 0.3) };
        var scores = new[] { DimensionScore.Create(PerformanceDimension.Accuracy, 0.9, 0.7, new[] { "test" }) };

        // Act
        var assessor = new BayesianSelfAssessor(beliefs, scores);

        // Assert
        var belief = assessor.GetCapabilityBelief("coding");
        belief.IsSome.Should().BeTrue();

        var dimResult = assessor.AssessDimensionAsync(PerformanceDimension.Accuracy).Result;
        dimResult.IsSuccess.Should().BeTrue();
        dimResult.Value.Score.Should().Be(0.9);
    }

    [Fact]
    public async Task AssessAsync_ReturnsSuccessWithAllDimensions()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = await assessor.AssessAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DimensionScores.Should().HaveCount(Enum.GetValues<PerformanceDimension>().Length);
    }

    [Fact]
    public async Task AssessDimensionAsync_ReturnsSuccessForValidDimension()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = await assessor.AssessDimensionAsync(PerformanceDimension.Speed);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Dimension.Should().Be(PerformanceDimension.Speed);
    }

    [Fact]
    public void GetCapabilityBelief_WithNullCapability_ReturnsNone()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.GetCapabilityBelief(null!);

        // Assert
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public void GetCapabilityBelief_WithEmptyCapability_ReturnsNone()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.GetCapabilityBelief("");

        // Assert
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public void GetCapabilityBelief_WithUnknownCapability_ReturnsNone()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.GetCapabilityBelief("nonexistent");

        // Assert
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public void UpdateBelief_WithValidEvidence_ReturnsUpdatedBelief()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.UpdateBelief("coding", 0.8);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CapabilityName.Should().Be("coding");
        result.Value.Proficiency.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void UpdateBelief_WithEmptyCapability_ReturnsFailure()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.UpdateBelief("", 0.5);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateBelief_WithInvalidEvidence_ReturnsFailure()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var resultTooHigh = assessor.UpdateBelief("test", 1.5);
        var resultTooLow = assessor.UpdateBelief("test", -0.1);

        // Assert
        resultTooHigh.IsFailure.Should().BeTrue();
        resultTooLow.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateBelief_MultipleUpdates_ConvergesOnEvidence()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act - repeatedly observe high success
        for (var i = 0; i < 10; i++)
        {
            assessor.UpdateBelief("coding", 0.9);
        }

        // Assert
        var belief = assessor.GetCapabilityBelief("coding");
        belief.IsSome.Should().BeTrue();
        belief.Value.Proficiency.Should().BeGreaterThan(0.7);
    }

    [Fact]
    public void UpdateBelief_CreatesNewBeliefIfNotExists()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        assessor.UpdateBelief("newCapability", 0.7);

        // Assert
        var belief = assessor.GetCapabilityBelief("newCapability");
        belief.IsSome.Should().BeTrue();
    }

    [Fact]
    public void GetAllBeliefs_ReturnsAllStoredBeliefs()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        assessor.UpdateBelief("coding", 0.8);
        assessor.UpdateBelief("reasoning", 0.7);

        // Act
        var beliefs = assessor.GetAllBeliefs();

        // Assert
        beliefs.Should().HaveCount(2);
        beliefs.Should().ContainKey("coding");
        beliefs.Should().ContainKey("reasoning");
    }

    [Fact]
    public void GetAllBeliefs_WithNoBeliefs_ReturnsEmpty()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var beliefs = assessor.GetAllBeliefs();

        // Assert
        beliefs.Should().BeEmpty();
    }

    [Fact]
    public void CalibrateConfidence_WithEmptySamples_ReturnsSuccess()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.CalibrateConfidence(Array.Empty<(double, double)>());

        // Assert
        result.IsSuccess.Should().BeTrue();
        assessor.GetCalibrationFactor().Should().Be(1.0); // unchanged
    }

    [Fact]
    public void CalibrateConfidence_WithOverconfidentSamples_ReducesFactor()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        var samples = new (double Predicted, double Actual)[]
        {
            (0.9, 0.5),
            (0.8, 0.4),
            (0.95, 0.6),
        };

        // Act
        assessor.CalibrateConfidence(samples);

        // Assert
        assessor.GetCalibrationFactor().Should().BeLessThan(1.0);
    }

    [Fact]
    public void CalibrateConfidence_WithUnderconfidentSamples_IncreasesFactor()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        var samples = new (double Predicted, double Actual)[]
        {
            (0.3, 0.7),
            (0.2, 0.6),
            (0.4, 0.8),
        };

        // Act
        assessor.CalibrateConfidence(samples);

        // Assert
        assessor.GetCalibrationFactor().Should().BeGreaterThan(1.0);
    }

    [Fact]
    public void GetCalibrationFactor_InitiallyOne()
    {
        // Arrange & Act
        var assessor = new BayesianSelfAssessor();

        // Assert
        assessor.GetCalibrationFactor().Should().Be(1.0);
    }

    [Fact]
    public void UpdateDimensionScore_UpdatesScoreForDimension()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.UpdateDimensionScore(
            PerformanceDimension.Accuracy, 0.9, 0.5, "good performance");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().BeGreaterThan(0.5);
        result.Value.Evidence.Should().Contain("good performance");
    }

    [Fact]
    public void UpdateDimensionScore_MultipleUpdates_AccumulatesEvidence()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        assessor.UpdateDimensionScore(PerformanceDimension.Speed, 0.8, 0.5, "fast response");
        var result = assessor.UpdateDimensionScore(PerformanceDimension.Speed, 0.9, 0.5, "even faster");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Evidence.Should().HaveCount(2);
    }

    [Fact]
    public async Task AssessDimensionAsync_AfterCalibration_AppliesCalibrationFactor()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        assessor.UpdateDimensionScore(PerformanceDimension.Accuracy, 0.8, 0.5, "evidence");

        // Calibrate to reduce confidence
        var samples = new (double, double)[] { (0.9, 0.5), (0.8, 0.3) };
        assessor.CalibrateConfidence(samples);

        // Act
        var result = await assessor.AssessDimensionAsync(PerformanceDimension.Accuracy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Confidence should be reduced by calibration factor
        assessor.GetCalibrationFactor().Should().BeLessThan(1.0);
    }
}

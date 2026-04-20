using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Tests.Metacognition;

[Trait("Category", "Unit")]
public sealed class DimensionScoreTests
{
    [Fact]
    public void Unknown_CreatesMaximumEntropyScore()
    {
        // Act
        var score = DimensionScore.Unknown(PerformanceDimension.Accuracy);

        // Assert
        score.Dimension.Should().Be(PerformanceDimension.Accuracy);
        score.Score.Should().Be(0.5);
        score.Confidence.Should().Be(0.0);
        score.Evidence.Should().BeEmpty();
        score.Trend.Should().Be(Trend.Unknown);
    }

    [Fact]
    public void Create_ClampsScoreToValidRange()
    {
        // Act
        var scoreTooHigh = DimensionScore.Create(PerformanceDimension.Speed, 1.5, 0.8, new[] { "fast" });
        var scoreTooLow = DimensionScore.Create(PerformanceDimension.Speed, -0.5, 0.8, new[] { "slow" });

        // Assert
        scoreTooHigh.Score.Should().Be(1.0);
        scoreTooLow.Score.Should().Be(0.0);
    }

    [Fact]
    public void Create_ClampsConfidenceToValidRange()
    {
        // Act
        var confTooHigh = DimensionScore.Create(PerformanceDimension.Speed, 0.5, 1.5, Array.Empty<string>());
        var confTooLow = DimensionScore.Create(PerformanceDimension.Speed, 0.5, -0.5, Array.Empty<string>());

        // Assert
        confTooHigh.Confidence.Should().Be(1.0);
        confTooLow.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Create_SetsUnknownTrend()
    {
        // Act
        var score = DimensionScore.Create(PerformanceDimension.Creativity, 0.7, 0.5, new[] { "evidence1" });

        // Assert
        score.Trend.Should().Be(Trend.Unknown);
        score.Evidence.Should().HaveCount(1);
    }

    [Fact]
    public void WithBayesianUpdate_UpdatesScoreAndConfidence()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Accuracy, 0.5, 0.5, new[] { "initial" });

        // Act
        var updated = score.WithBayesianUpdate(0.9, 0.5, "new evidence");

        // Assert
        updated.Score.Should().BeGreaterThan(0.5);
        updated.Score.Should().BeLessThanOrEqualTo(1.0);
        updated.Confidence.Should().BeGreaterThanOrEqualTo(score.Confidence);
        updated.Evidence.Should().HaveCount(2);
        updated.Evidence.Should().Contain("new evidence");
    }

    [Fact]
    public void WithBayesianUpdate_WithHigherScore_SetsImprovingTrend()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Accuracy, 0.3, 0.5, new[] { "initial" });

        // Act
        var updated = score.WithBayesianUpdate(0.9, 0.5, "improved performance");

        // Assert
        updated.Trend.Should().Be(Trend.Improving);
    }

    [Fact]
    public void WithBayesianUpdate_WithLowerScore_SetsDecliningTrend()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Accuracy, 0.8, 0.5, new[] { "initial" });

        // Act
        var updated = score.WithBayesianUpdate(0.1, 0.5, "degraded performance");

        // Assert
        updated.Trend.Should().Be(Trend.Declining);
    }

    [Fact]
    public void WithBayesianUpdate_WithSimilarScore_MaintainsPreviousTrend()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Accuracy, 0.5, 0.5, new[] { "initial" });

        // Act - small change within threshold
        var updated = score.WithBayesianUpdate(0.51, 0.5, "stable performance");

        // Assert - should be Stable since previous was Unknown and delta is small
        updated.Trend.Should().Be(Trend.Stable);
    }

    [Fact]
    public void WithBayesianUpdate_WithZeroConfidencePrior_UsesNewScore()
    {
        // Arrange
        var score = DimensionScore.Unknown(PerformanceDimension.Speed);

        // Act
        var updated = score.WithBayesianUpdate(0.8, 0.5, "first observation");

        // Assert
        updated.Score.Should().Be(0.8);
    }

    [Fact]
    public void Validate_WithValidValues_ReturnsSuccess()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Accuracy, 0.7, 0.5, new[] { "test" });

        // Act
        var result = score.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithScoreOutOfRange_ReturnsFailure()
    {
        // Arrange - construct directly to bypass clamping
        var score = new DimensionScore(PerformanceDimension.Accuracy, 1.5, 0.5, ImmutableList<string>.Empty, Trend.Stable);

        // Act
        var result = score.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithConfidenceOutOfRange_ReturnsFailure()
    {
        // Arrange
        var score = new DimensionScore(PerformanceDimension.Accuracy, 0.5, -0.1, ImmutableList<string>.Empty, Trend.Stable);

        // Act
        var result = score.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var evidence = ImmutableList.Create("test");
        var score1 = new DimensionScore(PerformanceDimension.Accuracy, 0.7, 0.5, evidence, Trend.Stable);
        var score2 = new DimensionScore(PerformanceDimension.Accuracy, 0.7, 0.5, evidence, Trend.Stable);

        // Assert
        score1.Should().Be(score2);
    }
}

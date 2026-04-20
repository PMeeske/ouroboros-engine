using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class OnlineLearningMetricsTests
{
    [Fact]
    public void Empty_ReturnsZeroedMetrics()
    {
        // Act
        var metrics = OnlineLearningMetrics.Empty;

        // Assert
        metrics.ProcessedCount.Should().Be(0);
        metrics.AverageScore.Should().Be(0.0);
        metrics.ScoreVariance.Should().Be(0.0);
        metrics.UpdateCount.Should().Be(0);
        metrics.AverageGradientMagnitude.Should().Be(0.0);
        metrics.ConvergenceMetric.Should().Be(1.0);
        metrics.LastUpdateTime.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void WithNewScore_UpdatesCountAndAverage()
    {
        // Arrange
        var metrics = OnlineLearningMetrics.Empty;

        // Act
        var updated = metrics.WithNewScore(0.8);

        // Assert
        updated.ProcessedCount.Should().Be(1);
        updated.AverageScore.Should().Be(0.8);
        updated.LastUpdateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void WithNewScore_MultipleUpdates_ComputesRunningAverage()
    {
        // Arrange
        var metrics = OnlineLearningMetrics.Empty;

        // Act
        metrics = metrics.WithNewScore(1.0);
        metrics = metrics.WithNewScore(3.0);

        // Assert
        metrics.ProcessedCount.Should().Be(2);
        metrics.AverageScore.Should().BeApproximately(2.0, 0.001);
    }

    [Fact]
    public void WithNewScore_UpdatesVariance()
    {
        // Arrange
        var metrics = OnlineLearningMetrics.Empty;

        // Act
        metrics = metrics.WithNewScore(1.0);
        metrics = metrics.WithNewScore(3.0);
        metrics = metrics.WithNewScore(5.0);

        // Assert
        metrics.ScoreVariance.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WithGradient_UpdatesGradientMetrics()
    {
        // Arrange
        var metrics = OnlineLearningMetrics.Empty;

        // Act
        var updated = metrics.WithGradient(0.5);

        // Assert
        updated.UpdateCount.Should().Be(1);
        updated.AverageGradientMagnitude.Should().Be(0.5);
        updated.LastUpdateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void WithGradient_MultipleUpdates_ComputesRunningAverage()
    {
        // Arrange
        var metrics = OnlineLearningMetrics.Empty;

        // Act
        metrics = metrics.WithGradient(1.0);
        metrics = metrics.WithGradient(3.0);

        // Assert
        metrics.UpdateCount.Should().Be(2);
        metrics.AverageGradientMagnitude.Should().BeApproximately(2.0, 0.001);
    }

    [Fact]
    public void ComputePerformanceScore_WithNoProcessed_ReturnsZero()
    {
        // Act
        var score = OnlineLearningMetrics.Empty.ComputePerformanceScore();

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public void ComputePerformanceScore_WithPositiveScore_ReturnsPositive()
    {
        // Arrange
        var metrics = OnlineLearningMetrics.Empty.WithNewScore(0.8);

        // Act
        var score = metrics.ComputePerformanceScore();

        // Assert
        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ComputePerformanceScore_HigherScoreMeansHigherPerformance()
    {
        // Arrange
        var low = OnlineLearningMetrics.Empty.WithNewScore(0.1);
        var high = OnlineLearningMetrics.Empty.WithNewScore(0.9);

        // Act & Assert
        high.ComputePerformanceScore().Should().BeGreaterThan(low.ComputePerformanceScore());
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var metrics = new OnlineLearningMetrics(10, 0.5, 0.1, 5, 0.3, 0.2, now);

        // Assert
        metrics.ProcessedCount.Should().Be(10);
        metrics.AverageScore.Should().Be(0.5);
        metrics.ScoreVariance.Should().Be(0.1);
        metrics.UpdateCount.Should().Be(5);
        metrics.AverageGradientMagnitude.Should().Be(0.3);
        metrics.ConvergenceMetric.Should().Be(0.2);
        metrics.LastUpdateTime.Should().Be(now);
    }
}

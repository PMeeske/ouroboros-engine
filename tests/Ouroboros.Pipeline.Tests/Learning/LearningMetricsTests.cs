using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class LearningMetricsTests
{
    [Fact]
    public void Empty_ReturnsZeroedMetrics()
    {
        // Act
        var metrics = LearningMetrics.Empty;

        // Assert
        metrics.TotalEpisodes.Should().Be(0);
        metrics.AverageReward.Should().Be(0.0);
        metrics.RewardVariance.Should().Be(0.0);
        metrics.ConvergenceRate.Should().Be(1.0);
        metrics.LearningEfficiency.Should().Be(0.0);
        metrics.Timestamps.Should().BeEmpty();
    }

    [Fact]
    public void FromRewards_WithEmptyList_ReturnsEmpty()
    {
        // Act
        var metrics = LearningMetrics.FromRewards(Array.Empty<double>());

        // Assert
        metrics.TotalEpisodes.Should().Be(0);
    }

    [Fact]
    public void FromRewards_ComputesCorrectAverage()
    {
        // Arrange
        var rewards = new[] { 0.1, 0.2, 0.3, 0.4, 0.5 };

        // Act
        var metrics = LearningMetrics.FromRewards(rewards);

        // Assert
        metrics.TotalEpisodes.Should().Be(5);
        metrics.AverageReward.Should().BeApproximately(0.3, 0.001);
    }

    [Fact]
    public void FromRewards_ComputesCorrectVariance()
    {
        // Arrange
        var rewards = new[] { 1.0, 1.0, 1.0 };

        // Act
        var metrics = LearningMetrics.FromRewards(rewards);

        // Assert
        metrics.RewardVariance.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void FromRewards_SetsTimestamp()
    {
        // Act
        var metrics = LearningMetrics.FromRewards(new[] { 0.5 });

        // Assert
        metrics.Timestamps.Should().HaveCount(1);
    }

    [Fact]
    public void WithNewReward_UpdatesAverageCorrectly()
    {
        // Arrange
        var metrics = LearningMetrics.Empty;

        // Act
        var updated = metrics.WithNewReward(1.0);

        // Assert
        updated.TotalEpisodes.Should().Be(1);
        updated.AverageReward.Should().Be(1.0);
        updated.Timestamps.Should().HaveCount(1);
    }

    [Fact]
    public void WithNewReward_MultipleUpdates_ComputesRunningAverage()
    {
        // Arrange
        var metrics = LearningMetrics.Empty;

        // Act
        metrics = metrics.WithNewReward(1.0);
        metrics = metrics.WithNewReward(3.0);

        // Assert
        metrics.TotalEpisodes.Should().Be(2);
        metrics.AverageReward.Should().BeApproximately(2.0, 0.001);
    }

    [Fact]
    public void WithNewReward_UpdatesVarianceWithWelfordAlgorithm()
    {
        // Arrange
        var metrics = LearningMetrics.Empty;

        // Act
        metrics = metrics.WithNewReward(1.0);
        metrics = metrics.WithNewReward(3.0);
        metrics = metrics.WithNewReward(5.0);

        // Assert
        metrics.RewardVariance.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WithNewReward_UpdatesConvergenceRate()
    {
        // Arrange
        var metrics = LearningMetrics.Empty;

        // Act
        var updated = metrics.WithNewReward(0.5);

        // Assert
        // ConvergenceRate = 0.9 * 1.0 + 0.1 * |0.5 - 0| = 0.9 + 0.05 = 0.95
        updated.ConvergenceRate.Should().BeApproximately(0.95, 0.01);
    }

    [Fact]
    public void ComputePerformanceScore_WithNoEpisodes_ReturnsZero()
    {
        // Act
        var score = LearningMetrics.Empty.ComputePerformanceScore();

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public void ComputePerformanceScore_WithPositiveRewards_ReturnsPositiveScore()
    {
        // Arrange
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6, 0.7 });

        // Act
        var score = metrics.ComputePerformanceScore();

        // Assert
        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ComputePerformanceScore_HigherRewardsMeanHigherScore()
    {
        // Arrange
        var lowMetrics = LearningMetrics.FromRewards(new[] { 0.1, 0.1, 0.1 });
        var highMetrics = LearningMetrics.FromRewards(new[] { 0.9, 0.9, 0.9 });

        // Act
        var lowScore = lowMetrics.ComputePerformanceScore();
        var highScore = highMetrics.ComputePerformanceScore();

        // Assert
        highScore.Should().BeGreaterThan(lowScore);
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var timestamps = ImmutableList.Create(DateTime.UtcNow);

        // Act
        var metrics = new LearningMetrics(10, 0.5, 0.1, 0.3, 0.2, timestamps);

        // Assert
        metrics.TotalEpisodes.Should().Be(10);
        metrics.AverageReward.Should().Be(0.5);
        metrics.RewardVariance.Should().Be(0.1);
        metrics.ConvergenceRate.Should().Be(0.3);
        metrics.LearningEfficiency.Should().Be(0.2);
        metrics.Timestamps.Should().HaveCount(1);
    }
}

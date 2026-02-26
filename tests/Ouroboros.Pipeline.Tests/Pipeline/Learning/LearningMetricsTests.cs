namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class LearningMetricsTests
{
    [Fact]
    public void Empty_HasZeroValues()
    {
        var metrics = LearningMetrics.Empty;

        metrics.AverageReward.Should().Be(0.0);
        metrics.TotalEpisodes.Should().Be(0);
        metrics.RewardVariance.Should().Be(0.0);
        metrics.ConvergenceRate.Should().Be(1.0);
        metrics.LearningEfficiency.Should().Be(0.0);
        metrics.Timestamps.Should().BeEmpty();
    }

    [Fact]
    public void FromRewards_ComputesMetrics()
    {
        var rewards = new List<double> { 0.5, 0.7, 0.9 };
        var metrics = LearningMetrics.FromRewards(rewards);

        metrics.TotalEpisodes.Should().Be(3);
        metrics.AverageReward.Should().BeApproximately(0.7, 0.001);
    }

    [Fact]
    public void FromRewards_EmptyList_ReturnsEmpty()
    {
        var metrics = LearningMetrics.FromRewards(Array.Empty<double>());

        metrics.TotalEpisodes.Should().Be(0);
        metrics.AverageReward.Should().Be(0.0);
    }

    [Fact]
    public void WithNewReward_UpdatesTotalEpisodesAndAverage()
    {
        var metrics = LearningMetrics.Empty;
        var updated = metrics.WithNewReward(0.8);

        updated.TotalEpisodes.Should().Be(1);
        updated.AverageReward.Should().Be(0.8);
    }

    [Fact]
    public void WithNewReward_AccumulatesMultipleRewards()
    {
        var metrics = LearningMetrics.Empty
            .WithNewReward(0.5)
            .WithNewReward(1.0);

        metrics.TotalEpisodes.Should().Be(2);
        metrics.AverageReward.Should().BeApproximately(0.75, 0.001);
    }

    [Fact]
    public void WithNewReward_AddsTimestamp()
    {
        var metrics = LearningMetrics.Empty.WithNewReward(0.5);

        metrics.Timestamps.Should().HaveCount(1);
    }

    [Fact]
    public void ComputePerformanceScore_ReturnsZeroForEmpty()
    {
        var score = LearningMetrics.Empty.ComputePerformanceScore();

        score.Should().Be(0.0);
    }

    [Fact]
    public void ComputePerformanceScore_ReturnsReasonableValue()
    {
        var metrics = LearningMetrics.FromRewards(new List<double> { 0.5, 0.7, 0.9 });
        var score = metrics.ComputePerformanceScore();

        score.Should().BeGreaterThanOrEqualTo(-1.0);
        score.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void FromRewards_ComputesVariance()
    {
        var rewards = new List<double> { 1.0, 1.0, 1.0 };
        var metrics = LearningMetrics.FromRewards(rewards);

        metrics.RewardVariance.Should().BeApproximately(0.0, 0.001);
    }
}

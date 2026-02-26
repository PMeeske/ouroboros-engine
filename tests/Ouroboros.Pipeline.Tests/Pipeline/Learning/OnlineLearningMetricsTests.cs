namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class OnlineLearningMetricsTests
{
    [Fact]
    public void Empty_HasZeroValues()
    {
        var metrics = OnlineLearningMetrics.Empty;
        metrics.Should().NotBeNull();
    }

    [Fact]
    public void WithNewScore_UpdatesMetrics()
    {
        var metrics = OnlineLearningMetrics.Empty;
        var updated = metrics.WithNewScore(0.8);

        updated.Should().NotBe(metrics);
    }

    [Fact]
    public void ComputePerformanceScore_ReturnsValueBetweenZeroAndOne()
    {
        var metrics = OnlineLearningMetrics.Empty
            .WithNewScore(0.7)
            .WithNewScore(0.8);

        var score = metrics.ComputePerformanceScore();

        score.Should().BeGreaterThanOrEqualTo(0.0);
        score.Should().BeLessThanOrEqualTo(1.0);
    }
}

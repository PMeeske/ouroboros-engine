namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class CognitiveHealthTests
{
    [Fact]
    public void Optimal_HasFullHealthScore()
    {
        var health = CognitiveHealth.Optimal();

        health.HealthScore.Should().Be(1.0);
        health.ProcessingEfficiency.Should().Be(1.0);
        health.ErrorRate.Should().Be(0.0);
        health.Status.Should().Be(HealthStatus.Healthy);
        health.ActiveAlerts.Should().BeEmpty();
    }

    [Fact]
    public void FromMetrics_DeterminesHealthyStatus()
    {
        var health = CognitiveHealth.FromMetrics(
            0.9, 0.8, 0.05, TimeSpan.FromMilliseconds(50),
            ImmutableList<MonitoringAlert>.Empty);

        health.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void FromMetrics_DeterminesCriticalStatus_WhenHealthScoreLow()
    {
        var health = CognitiveHealth.FromMetrics(
            0.2, 0.8, 0.1, TimeSpan.FromMilliseconds(50),
            ImmutableList<MonitoringAlert>.Empty);

        health.Status.Should().Be(HealthStatus.Critical);
    }

    [Fact]
    public void FromMetrics_DeterminesCriticalStatus_WhenErrorRateHigh()
    {
        var health = CognitiveHealth.FromMetrics(
            0.8, 0.8, 0.6, TimeSpan.FromMilliseconds(50),
            ImmutableList<MonitoringAlert>.Empty);

        health.Status.Should().Be(HealthStatus.Critical);
    }

    [Fact]
    public void RequiresAttention_ReturnsTrueForNonHealthy()
    {
        var health = CognitiveHealth.FromMetrics(
            0.4, 0.4, 0.35, TimeSpan.FromMilliseconds(50),
            ImmutableList<MonitoringAlert>.Empty);

        health.RequiresAttention().Should().BeTrue();
    }

    [Fact]
    public void RequiresAttention_ReturnsFalseForHealthy()
    {
        CognitiveHealth.Optimal().RequiresAttention().Should().BeFalse();
    }

    [Fact]
    public void IsCritical_ReturnsTrueForCriticalStatus()
    {
        var health = CognitiveHealth.FromMetrics(
            0.1, 0.1, 0.9, TimeSpan.FromMilliseconds(50),
            ImmutableList<MonitoringAlert>.Empty);

        health.IsCritical().Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsSuccess_ForValidHealth()
    {
        var result = CognitiveHealth.Optimal().Validate();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void FromMetrics_ClampsValues()
    {
        var health = CognitiveHealth.FromMetrics(
            1.5, -0.5, -1.0, TimeSpan.Zero,
            ImmutableList<MonitoringAlert>.Empty);

        health.HealthScore.Should().Be(1.0);
        health.ProcessingEfficiency.Should().Be(0.0);
        health.ErrorRate.Should().Be(0.0);
    }
}

using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Tests.Metacognition;

[Trait("Category", "Unit")]
public sealed class CognitiveHealthTests
{
    [Fact]
    public void Optimal_ReturnsHealthyWithPerfectMetrics()
    {
        // Act
        var health = CognitiveHealth.Optimal();

        // Assert
        health.HealthScore.Should().Be(1.0);
        health.ProcessingEfficiency.Should().Be(1.0);
        health.ErrorRate.Should().Be(0.0);
        health.ResponseLatency.Should().Be(TimeSpan.Zero);
        health.ActiveAlerts.Should().BeEmpty();
        health.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void FromMetrics_WithGoodMetrics_ReturnsHealthyStatus()
    {
        // Act
        var health = CognitiveHealth.FromMetrics(0.9, 0.8, 0.01, TimeSpan.FromMilliseconds(50),
            ImmutableList<MonitoringAlert>.Empty);

        // Assert
        health.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void FromMetrics_WithDegradedMetrics_ReturnsDegradedStatus()
    {
        // Act - health < 0.7, efficiency ok, error rate > 0.1
        var health = CognitiveHealth.FromMetrics(0.65, 0.7, 0.15, TimeSpan.FromMilliseconds(100),
            ImmutableList<MonitoringAlert>.Empty);

        // Assert
        health.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void FromMetrics_WithLowHealthScore_ReturnsImpairedStatus()
    {
        // Act
        var health = CognitiveHealth.FromMetrics(0.45, 0.5, 0.2, TimeSpan.FromMilliseconds(200),
            ImmutableList<MonitoringAlert>.Empty);

        // Assert
        health.Status.Should().Be(HealthStatus.Impaired);
    }

    [Fact]
    public void FromMetrics_WithCriticalHealthScore_ReturnsCriticalStatus()
    {
        // Act
        var health = CognitiveHealth.FromMetrics(0.2, 0.3, 0.6, TimeSpan.FromMilliseconds(500),
            ImmutableList<MonitoringAlert>.Empty);

        // Assert
        health.Status.Should().Be(HealthStatus.Critical);
    }

    [Fact]
    public void FromMetrics_WithCriticalAlertPriority9_ReturnsCriticalStatus()
    {
        // Arrange
        var criticalAlert = new MonitoringAlert(
            Guid.NewGuid(), "Critical", "Critical alert",
            ImmutableList<CognitiveEvent>.Empty, "Take action", 9, DateTime.UtcNow);

        // Act
        var health = CognitiveHealth.FromMetrics(0.9, 0.9, 0.0, TimeSpan.Zero,
            ImmutableList.Create(criticalAlert));

        // Assert
        health.Status.Should().Be(HealthStatus.Critical);
    }

    [Fact]
    public void FromMetrics_WithTwoHighPriorityAlerts_ReturnsImpairedStatus()
    {
        // Arrange
        var alert1 = new MonitoringAlert(
            Guid.NewGuid(), "High1", "Alert 1",
            ImmutableList<CognitiveEvent>.Empty, "Action", 7, DateTime.UtcNow);
        var alert2 = new MonitoringAlert(
            Guid.NewGuid(), "High2", "Alert 2",
            ImmutableList<CognitiveEvent>.Empty, "Action", 8, DateTime.UtcNow);

        // Act
        var health = CognitiveHealth.FromMetrics(0.9, 0.9, 0.0, TimeSpan.Zero,
            ImmutableList.Create(alert1, alert2));

        // Assert
        health.Status.Should().Be(HealthStatus.Impaired);
    }

    [Fact]
    public void FromMetrics_ClampsValues()
    {
        // Act
        var health = CognitiveHealth.FromMetrics(1.5, 2.0, -0.5, TimeSpan.Zero,
            ImmutableList<MonitoringAlert>.Empty);

        // Assert
        health.HealthScore.Should().Be(1.0);
        health.ProcessingEfficiency.Should().Be(1.0);
        health.ErrorRate.Should().Be(0.0);
    }

    [Fact]
    public void RequiresAttention_WhenHealthy_ReturnsFalse()
    {
        // Arrange
        var health = CognitiveHealth.Optimal();

        // Act & Assert
        health.RequiresAttention().Should().BeFalse();
    }

    [Fact]
    public void RequiresAttention_WhenDegraded_ReturnsTrue()
    {
        // Arrange
        var health = CognitiveHealth.FromMetrics(0.65, 0.7, 0.15, TimeSpan.Zero,
            ImmutableList<MonitoringAlert>.Empty);

        // Act & Assert
        health.RequiresAttention().Should().BeTrue();
    }

    [Fact]
    public void IsCritical_WhenCritical_ReturnsTrue()
    {
        // Arrange
        var health = CognitiveHealth.FromMetrics(0.1, 0.1, 0.8, TimeSpan.Zero,
            ImmutableList<MonitoringAlert>.Empty);

        // Act & Assert
        health.IsCritical().Should().BeTrue();
    }

    [Fact]
    public void IsCritical_WhenHealthy_ReturnsFalse()
    {
        // Act & Assert
        CognitiveHealth.Optimal().IsCritical().Should().BeFalse();
    }

    [Fact]
    public void Validate_WithValidValues_ReturnsSuccess()
    {
        // Act
        var result = CognitiveHealth.Optimal().Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidHealthScore_ReturnsFailure()
    {
        // Arrange - construct directly to bypass clamping
        var health = new CognitiveHealth(DateTime.UtcNow, 1.5, 0.5, 0.0, TimeSpan.Zero,
            ImmutableList<MonitoringAlert>.Empty, HealthStatus.Healthy);

        // Act
        var result = health.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNegativeErrorRate_ReturnsFailure()
    {
        // Arrange
        var health = new CognitiveHealth(DateTime.UtcNow, 0.5, 0.5, -0.1, TimeSpan.Zero,
            ImmutableList<MonitoringAlert>.Empty, HealthStatus.Healthy);

        // Act
        var result = health.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}

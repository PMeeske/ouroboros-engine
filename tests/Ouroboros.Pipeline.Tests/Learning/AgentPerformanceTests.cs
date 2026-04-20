using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class AgentPerformanceTests
{
    [Fact]
    public void Initial_CreatesZeroedMetrics()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var perf = AgentPerformance.Initial(agentId);

        // Assert
        perf.AgentId.Should().Be(agentId);
        perf.TotalInteractions.Should().Be(0);
        perf.SuccessRate.Should().Be(0.0);
        perf.AverageResponseQuality.Should().Be(0.0);
        perf.LearningCurve.Should().BeEmpty();
        perf.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void WithLearningCurveEntry_AddsEntryToCurve()
    {
        // Arrange
        var perf = AgentPerformance.Initial(Guid.NewGuid());

        // Act
        var updated = perf.WithLearningCurveEntry(0.8);

        // Assert
        updated.LearningCurve.Should().HaveCount(1);
        updated.LearningCurve[0].Should().Be(0.8);
    }

    [Fact]
    public void WithLearningCurveEntry_TrimsWhenExceedingMaxLength()
    {
        // Arrange
        var perf = AgentPerformance.Initial(Guid.NewGuid());
        for (int i = 0; i < 10; i++)
        {
            perf = perf.WithLearningCurveEntry(i * 0.1, maxCurveLength: 5);
        }

        // Assert
        perf.LearningCurve.Should().HaveCount(5);
        perf.LearningCurve[0].Should().Be(0.5); // Values 5-9 remain
    }

    [Fact]
    public void CalculateTrend_WithFewerThanTwoEntries_ReturnsZero()
    {
        // Arrange
        var perf = AgentPerformance.Initial(Guid.NewGuid());

        // Act & Assert
        perf.CalculateTrend().Should().Be(0.0);

        var singleEntry = perf.WithLearningCurveEntry(0.5);
        singleEntry.CalculateTrend().Should().Be(0.0);
    }

    [Fact]
    public void CalculateTrend_WithIncreasingValues_ReturnsPositive()
    {
        // Arrange
        var perf = AgentPerformance.Initial(Guid.NewGuid());
        for (int i = 0; i < 5; i++)
        {
            perf = perf.WithLearningCurveEntry(i * 0.2);
        }

        // Act
        var trend = perf.CalculateTrend();

        // Assert
        trend.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateTrend_WithDecreasingValues_ReturnsNegative()
    {
        // Arrange
        var perf = AgentPerformance.Initial(Guid.NewGuid());
        for (int i = 5; i > 0; i--)
        {
            perf = perf.WithLearningCurveEntry(i * 0.2);
        }

        // Act
        var trend = perf.CalculateTrend();

        // Assert
        trend.Should().BeLessThan(0);
    }

    [Fact]
    public void CalculateTrend_RespectsWindowSize()
    {
        // Arrange
        var perf = AgentPerformance.Initial(Guid.NewGuid());
        // Add 20 entries: first 10 increasing, last 10 decreasing
        for (int i = 0; i < 10; i++)
            perf = perf.WithLearningCurveEntry(i * 0.1);
        for (int i = 10; i > 0; i--)
            perf = perf.WithLearningCurveEntry(i * 0.1);

        // Act
        var trendSmallWindow = perf.CalculateTrend(windowSize: 5);

        // Assert - last 5 are decreasing
        trendSmallWindow.Should().BeLessThan(0);
    }

    [Fact]
    public void IsStagnating_WithFewEntries_ReturnsFalse()
    {
        // Arrange
        var perf = AgentPerformance.Initial(Guid.NewGuid());
        perf = perf.WithLearningCurveEntry(0.5);

        // Act & Assert
        perf.IsStagnating(windowSize: 10).Should().BeFalse();
    }

    [Fact]
    public void IsStagnating_WithConstantValues_ReturnsTrue()
    {
        // Arrange
        var perf = AgentPerformance.Initial(Guid.NewGuid());
        for (int i = 0; i < 15; i++)
        {
            perf = perf.WithLearningCurveEntry(0.5);
        }

        // Act & Assert
        perf.IsStagnating(windowSize: 10, varianceThreshold: 0.001).Should().BeTrue();
    }

    [Fact]
    public void IsStagnating_WithVaryingValues_ReturnsFalse()
    {
        // Arrange
        var perf = AgentPerformance.Initial(Guid.NewGuid());
        for (int i = 0; i < 15; i++)
        {
            perf = perf.WithLearningCurveEntry(i * 0.1);
        }

        // Act & Assert
        perf.IsStagnating(windowSize: 10, varianceThreshold: 0.001).Should().BeFalse();
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var curve = ImmutableList.Create(0.1, 0.2, 0.3);
        var now = DateTime.UtcNow;

        // Act
        var perf = new AgentPerformance(agentId, 50, 0.8, 0.7, curve, now);

        // Assert
        perf.AgentId.Should().Be(agentId);
        perf.TotalInteractions.Should().Be(50);
        perf.SuccessRate.Should().Be(0.8);
        perf.AverageResponseQuality.Should().Be(0.7);
        perf.LearningCurve.Should().HaveCount(3);
        perf.LastUpdated.Should().Be(now);
    }
}

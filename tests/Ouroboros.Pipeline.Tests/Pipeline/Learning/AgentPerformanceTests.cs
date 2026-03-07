namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class AgentPerformanceTests
{
    [Fact]
    public void Initial_HasDefaultValues()
    {
        var agentId = Guid.NewGuid();
        var perf = AgentPerformance.Initial(agentId);

        perf.AgentId.Should().Be(agentId);
        perf.TotalInteractions.Should().Be(0);
        perf.SuccessRate.Should().Be(0.0);
        perf.AverageResponseQuality.Should().Be(0.0);
        perf.LearningCurve.Should().BeEmpty();
    }

    [Fact]
    public void WithLearningCurveEntry_AddsEntry()
    {
        var perf = AgentPerformance.Initial(Guid.NewGuid());
        var updated = perf.WithLearningCurveEntry(0.8);

        updated.LearningCurve.Should().HaveCount(1);
        updated.LearningCurve[0].Should().Be(0.8);
    }

    [Fact]
    public void WithLearningCurveEntry_TrimsWhenExceedingMaxLength()
    {
        var perf = AgentPerformance.Initial(Guid.NewGuid());

        for (int i = 0; i < 5; i++)
        {
            perf = perf.WithLearningCurveEntry(i * 0.1, maxCurveLength: 3);
        }

        perf.LearningCurve.Should().HaveCount(3);
    }

    [Fact]
    public void CalculateTrend_ReturnsZeroForFewEntries()
    {
        var perf = AgentPerformance.Initial(Guid.NewGuid());

        perf.CalculateTrend().Should().Be(0.0);
    }

    [Fact]
    public void CalculateTrend_ReturnsPositiveForIncreasingValues()
    {
        var perf = AgentPerformance.Initial(Guid.NewGuid())
            .WithLearningCurveEntry(0.1)
            .WithLearningCurveEntry(0.3)
            .WithLearningCurveEntry(0.5)
            .WithLearningCurveEntry(0.7)
            .WithLearningCurveEntry(0.9);

        var trend = perf.CalculateTrend();

        trend.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void CalculateTrend_ReturnsNegativeForDecreasingValues()
    {
        var perf = AgentPerformance.Initial(Guid.NewGuid())
            .WithLearningCurveEntry(0.9)
            .WithLearningCurveEntry(0.7)
            .WithLearningCurveEntry(0.5)
            .WithLearningCurveEntry(0.3)
            .WithLearningCurveEntry(0.1);

        var trend = perf.CalculateTrend();

        trend.Should().BeLessThan(0.0);
    }

    [Fact]
    public void IsStagnating_ReturnsFalseForFewEntries()
    {
        var perf = AgentPerformance.Initial(Guid.NewGuid())
            .WithLearningCurveEntry(0.5);

        perf.IsStagnating().Should().BeFalse();
    }

    [Fact]
    public void IsStagnating_ReturnsTrueForConstantValues()
    {
        var perf = AgentPerformance.Initial(Guid.NewGuid());
        for (int i = 0; i < 10; i++)
        {
            perf = perf.WithLearningCurveEntry(0.5);
        }

        perf.IsStagnating().Should().BeTrue();
    }
}

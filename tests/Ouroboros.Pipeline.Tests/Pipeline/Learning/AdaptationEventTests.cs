namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class AdaptationEventTests
{
    private static AgentPerformance CreatePerformance(double quality = 0.5)
    {
        var perf = AgentPerformance.Initial(Guid.NewGuid());
        return perf with { AverageResponseQuality = quality };
    }

    [Fact]
    public void Create_SetsProperties()
    {
        var agentId = Guid.NewGuid();
        var beforeMetrics = CreatePerformance(0.7);

        var evt = AdaptationEvent.Create(
            agentId,
            AdaptationEventType.StrategyChange,
            "Changed to exploratory",
            beforeMetrics);

        evt.AgentId.Should().Be(agentId);
        evt.EventType.Should().Be(AdaptationEventType.StrategyChange);
        evt.Description.Should().Be("Changed to exploratory");
        evt.BeforeMetrics.Should().Be(beforeMetrics);
        evt.AfterMetrics.Should().BeNull();
        evt.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void WithAfterMetrics_SetsAfterMetricsValue()
    {
        var beforeMetrics = CreatePerformance(0.5);
        var afterMetrics = CreatePerformance(0.8);

        var evt = AdaptationEvent.Create(
            Guid.NewGuid(),
            AdaptationEventType.StrategyChange,
            "test",
            beforeMetrics);
        var updated = evt.WithAfterMetrics(afterMetrics);

        updated.AfterMetrics.Should().Be(afterMetrics);
    }

    [Fact]
    public void PerformanceDelta_ComputesDifferenceInResponseQuality()
    {
        var before = CreatePerformance(0.5);
        var after = CreatePerformance(0.8);

        var evt = AdaptationEvent.Create(
            Guid.NewGuid(),
            AdaptationEventType.StrategyChange,
            "test",
            before).WithAfterMetrics(after);

        evt.PerformanceDelta.Should().BeApproximately(0.3, 0.001);
    }

    [Fact]
    public void PerformanceDelta_IsNullWhenNoAfterMetrics()
    {
        var evt = AdaptationEvent.Create(
            Guid.NewGuid(),
            AdaptationEventType.StrategyChange,
            "test",
            CreatePerformance());

        evt.PerformanceDelta.Should().BeNull();
    }

    [Fact]
    public void WasBeneficial_ReturnsTrueWhenDeltaPositive()
    {
        var before = CreatePerformance(0.5);
        var after = CreatePerformance(0.8);

        var evt = AdaptationEvent.Create(
            Guid.NewGuid(),
            AdaptationEventType.StrategyChange,
            "test",
            before).WithAfterMetrics(after);

        evt.WasBeneficial.Should().BeTrue();
    }

    [Fact]
    public void WasBeneficial_ReturnsFalseWhenDeltaNegative()
    {
        var before = CreatePerformance(0.8);
        var after = CreatePerformance(0.5);

        var evt = AdaptationEvent.Create(
            Guid.NewGuid(),
            AdaptationEventType.StrategyChange,
            "test",
            before).WithAfterMetrics(after);

        evt.WasBeneficial.Should().BeFalse();
    }

    [Fact]
    public void WasBeneficial_IsNullWhenNoAfterMetrics()
    {
        var evt = AdaptationEvent.Create(
            Guid.NewGuid(),
            AdaptationEventType.StrategyChange,
            "test",
            CreatePerformance());

        evt.WasBeneficial.Should().BeNull();
    }
}

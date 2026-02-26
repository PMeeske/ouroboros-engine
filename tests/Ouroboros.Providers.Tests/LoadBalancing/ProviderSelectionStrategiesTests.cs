using Ouroboros.Providers.LoadBalancing.Strategies;

namespace Ouroboros.Tests.LoadBalancing;

[Trait("Category", "Unit")]
public sealed class ProviderSelectionStrategiesTests
{
    [Fact]
    public void RoundRobin_ReturnsRoundRobinStrategy()
    {
        var strategy = ProviderSelectionStrategies.RoundRobin;
        strategy.Should().BeOfType<RoundRobinStrategy>();
        strategy.Name.Should().Be("RoundRobin");
    }

    [Fact]
    public void LeastLatency_ReturnsLeastLatencyStrategy()
    {
        var strategy = ProviderSelectionStrategies.LeastLatency;
        strategy.Should().BeOfType<LeastLatencyStrategy>();
        strategy.Name.Should().Be("LeastLatency");
    }

    [Fact]
    public void AdaptiveHealth_ReturnsAdaptiveHealthStrategy()
    {
        var strategy = ProviderSelectionStrategies.AdaptiveHealth;
        strategy.Should().BeOfType<AdaptiveHealthStrategy>();
        strategy.Name.Should().Be("AdaptiveHealth");
    }

    [Fact]
    public void EachCall_ReturnsNewInstance()
    {
        var a = ProviderSelectionStrategies.RoundRobin;
        var b = ProviderSelectionStrategies.RoundRobin;
        a.Should().NotBeSameAs(b);
    }
}

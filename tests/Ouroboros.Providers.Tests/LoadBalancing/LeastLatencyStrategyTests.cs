using Ouroboros.Providers.LoadBalancing.Strategies;

namespace Ouroboros.Tests.LoadBalancing;

[Trait("Category", "Unit")]
public sealed class LeastLatencyStrategyTests
{
    private static ProviderHealthStatus CreateStatus(
        string providerId,
        double averageLatencyMs = 100.0) =>
        new(
            ProviderId: providerId,
            IsHealthy: true,
            SuccessRate: 0.95,
            AverageLatencyMs: averageLatencyMs,
            ConsecutiveFailures: 0,
            LastFailureTime: null,
            CooldownUntil: null,
            TotalRequests: 100,
            SuccessfulRequests: 95,
            LastChecked: DateTime.UtcNow);

    [Fact]
    public void Name_ReturnsLeastLatency()
    {
        var strategy = new LeastLatencyStrategy();
        strategy.Name.Should().Be("LeastLatency");
    }

    [Fact]
    public void SelectProvider_ReturnsLowestLatency()
    {
        var strategy = new LeastLatencyStrategy();
        var providers = new List<string> { "a", "b", "c" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["a"] = CreateStatus("a", averageLatencyMs: 500),
            ["b"] = CreateStatus("b", averageLatencyMs: 100),
            ["c"] = CreateStatus("c", averageLatencyMs: 300),
        };

        var selected = strategy.SelectProvider(providers, health);
        selected.Should().Be("b");
    }

    [Fact]
    public void SelectProvider_NullProviders_Throws()
    {
        var strategy = new LeastLatencyStrategy();
        FluentActions.Invoking(() => strategy.SelectProvider(null!, new Dictionary<string, ProviderHealthStatus>()))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SelectProvider_EmptyProviders_Throws()
    {
        var strategy = new LeastLatencyStrategy();
        FluentActions.Invoking(() => strategy.SelectProvider(new List<string>(), new Dictionary<string, ProviderHealthStatus>()))
            .Should().Throw<ArgumentException>();
    }
}

using Ouroboros.Providers.LoadBalancing.Strategies;

namespace Ouroboros.Tests.LoadBalancing;

[Trait("Category", "Unit")]
public sealed class AdaptiveHealthStrategyTests
{
    private static ProviderHealthStatus CreateStatus(
        string providerId,
        double healthScoreProxy = 0.5,
        double averageLatencyMs = 100.0) =>
        new(
            ProviderId: providerId,
            IsHealthy: true,
            SuccessRate: healthScoreProxy,
            AverageLatencyMs: averageLatencyMs,
            ConsecutiveFailures: 0,
            LastFailureTime: null,
            CooldownUntil: null,
            TotalRequests: 100,
            SuccessfulRequests: (int)(100 * healthScoreProxy),
            LastChecked: DateTime.UtcNow);

    [Fact]
    public void Name_ReturnsAdaptiveHealth()
    {
        var strategy = new AdaptiveHealthStrategy();
        strategy.Name.Should().Be("AdaptiveHealth");
    }

    [Fact]
    public void SelectProvider_ReturnsHighestHealthScore()
    {
        var strategy = new AdaptiveHealthStrategy();
        var providers = new List<string> { "a", "b", "c" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["a"] = CreateStatus("a", healthScoreProxy: 0.5),
            ["b"] = CreateStatus("b", healthScoreProxy: 0.9),
            ["c"] = CreateStatus("c", healthScoreProxy: 0.7),
        };

        var selected = strategy.SelectProvider(providers, health);
        selected.Should().Be("b");
    }

    [Fact]
    public void SelectProvider_NullProviders_Throws()
    {
        var strategy = new AdaptiveHealthStrategy();
        FluentActions.Invoking(() => strategy.SelectProvider(null!, new Dictionary<string, ProviderHealthStatus>()))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SelectProvider_EmptyProviders_Throws()
    {
        var strategy = new AdaptiveHealthStrategy();
        FluentActions.Invoking(() => strategy.SelectProvider(new List<string>(), new Dictionary<string, ProviderHealthStatus>()))
            .Should().Throw<ArgumentException>();
    }
}

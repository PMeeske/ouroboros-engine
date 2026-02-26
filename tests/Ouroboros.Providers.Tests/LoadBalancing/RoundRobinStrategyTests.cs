using Ouroboros.Providers.LoadBalancing.Strategies;

namespace Ouroboros.Tests.LoadBalancing;

[Trait("Category", "Unit")]
public sealed class RoundRobinStrategyTests
{
    private static ProviderHealthStatus CreateStatus(string providerId) =>
        new(
            ProviderId: providerId,
            IsHealthy: true,
            SuccessRate: 0.95,
            AverageLatencyMs: 100.0,
            ConsecutiveFailures: 0,
            LastFailureTime: null,
            CooldownUntil: null,
            TotalRequests: 100,
            SuccessfulRequests: 95,
            LastChecked: DateTime.UtcNow);

    [Fact]
    public void Name_ReturnsRoundRobin()
    {
        var strategy = new RoundRobinStrategy();
        strategy.Name.Should().Be("RoundRobin");
    }

    [Fact]
    public void SelectProvider_CyclesThroughProviders()
    {
        var strategy = new RoundRobinStrategy();
        var providers = new List<string> { "a", "b", "c" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["a"] = CreateStatus("a"),
            ["b"] = CreateStatus("b"),
            ["c"] = CreateStatus("c"),
        };

        strategy.SelectProvider(providers, health).Should().Be("a");
        strategy.SelectProvider(providers, health).Should().Be("b");
        strategy.SelectProvider(providers, health).Should().Be("c");
        strategy.SelectProvider(providers, health).Should().Be("a"); // wraps around
    }

    [Fact]
    public void SelectProvider_NullProviders_Throws()
    {
        var strategy = new RoundRobinStrategy();
        FluentActions.Invoking(() => strategy.SelectProvider(null!, new Dictionary<string, ProviderHealthStatus>()))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SelectProvider_EmptyProviders_Throws()
    {
        var strategy = new RoundRobinStrategy();
        FluentActions.Invoking(() => strategy.SelectProvider(new List<string>(), new Dictionary<string, ProviderHealthStatus>()))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SelectProvider_SingleProvider_AlwaysReturnsSame()
    {
        var strategy = new RoundRobinStrategy();
        var providers = new List<string> { "only" };
        var health = new Dictionary<string, ProviderHealthStatus> { ["only"] = CreateStatus("only") };

        for (int i = 0; i < 5; i++)
        {
            strategy.SelectProvider(providers, health).Should().Be("only");
        }
    }
}

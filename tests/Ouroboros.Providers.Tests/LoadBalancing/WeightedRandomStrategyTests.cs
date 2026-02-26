using Ouroboros.Providers.LoadBalancing.Strategies;

namespace Ouroboros.Tests.LoadBalancing;

[Trait("Category", "Unit")]
public sealed class WeightedRandomStrategyTests
{
    private static ProviderHealthStatus CreateStatus(
        string providerId,
        double successRate = 0.95,
        double averageLatencyMs = 100.0) =>
        new(
            ProviderId: providerId,
            IsHealthy: true,
            SuccessRate: successRate,
            AverageLatencyMs: averageLatencyMs,
            ConsecutiveFailures: 0,
            LastFailureTime: null,
            CooldownUntil: null,
            TotalRequests: 100,
            SuccessfulRequests: (int)(100 * successRate),
            LastChecked: DateTime.UtcNow);

    [Fact]
    public void Name_ReturnsWeightedRandom()
    {
        var strategy = new WeightedRandomStrategy();
        strategy.Name.Should().Be("WeightedRandom");
    }

    [Fact]
    public void SelectProvider_NullProviders_Throws()
    {
        var strategy = new WeightedRandomStrategy();
        FluentActions.Invoking(() => strategy.SelectProvider(null!, new Dictionary<string, ProviderHealthStatus>()))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SelectProvider_EmptyProviders_Throws()
    {
        var strategy = new WeightedRandomStrategy();
        FluentActions.Invoking(() => strategy.SelectProvider(new List<string>(), new Dictionary<string, ProviderHealthStatus>()))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SelectProvider_SingleProvider_ReturnsThatProvider()
    {
        var strategy = new WeightedRandomStrategy();
        var providers = new List<string> { "only" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["only"] = CreateStatus("only"),
        };

        var selected = strategy.SelectProvider(providers, health);
        selected.Should().Be("only");
    }

    [Fact]
    public void SelectProvider_MultipleProviders_ReturnsOneOfThem()
    {
        var strategy = new WeightedRandomStrategy();
        var providers = new List<string> { "a", "b", "c" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["a"] = CreateStatus("a", successRate: 0.9),
            ["b"] = CreateStatus("b", successRate: 0.8),
            ["c"] = CreateStatus("c", successRate: 0.7),
        };

        var selected = strategy.SelectProvider(providers, health);
        providers.Should().Contain(selected);
    }
}

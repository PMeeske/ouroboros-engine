// <copyright file="RoundRobinStrategyTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Providers.LoadBalancing.Strategies;

namespace Ouroboros.Tests.Providers.LoadBalancing;

/// <summary>
/// Unit tests for <see cref="RoundRobinStrategy"/>.
/// </summary>
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
        // Arrange
        var strategy = new RoundRobinStrategy();

        // Act
        var name = strategy.Name;

        // Assert
        name.Should().Be("RoundRobin");
    }

    [Fact]
    public void SelectProvider_CyclesThroughProviders()
    {
        // Arrange
        var strategy = new RoundRobinStrategy();
        var providers = new List<string> { "a", "b", "c" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["a"] = CreateStatus("a"),
            ["b"] = CreateStatus("b"),
            ["c"] = CreateStatus("c"),
        };

        // Act & Assert
        strategy.SelectProvider(providers, health).Should().Be("a");
        strategy.SelectProvider(providers, health).Should().Be("b");
        strategy.SelectProvider(providers, health).Should().Be("c");
        strategy.SelectProvider(providers, health).Should().Be("a"); // wraps around
    }

    [Fact]
    public void SelectProvider_NullProviders_ThrowsArgumentException()
    {
        // Arrange
        var strategy = new RoundRobinStrategy();

        // Act
        Action act = () => strategy.SelectProvider(null!, new Dictionary<string, ProviderHealthStatus>());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("healthyProviders");
    }

    [Fact]
    public void SelectProvider_EmptyProviders_ThrowsArgumentException()
    {
        // Arrange
        var strategy = new RoundRobinStrategy();

        // Act
        Action act = () => strategy.SelectProvider(new List<string>(), new Dictionary<string, ProviderHealthStatus>());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("healthyProviders");
    }

    [Fact]
    public void SelectProvider_SingleProvider_AlwaysReturnsSame()
    {
        // Arrange
        var strategy = new RoundRobinStrategy();
        var providers = new List<string> { "only" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["only"] = CreateStatus("only"),
        };

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            strategy.SelectProvider(providers, health).Should().Be("only");
        }
    }

    [Fact]
    public void SelectProvider_TwoProviders_Alternates()
    {
        // Arrange
        var strategy = new RoundRobinStrategy();
        var providers = new List<string> { "x", "y" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["x"] = CreateStatus("x"),
            ["y"] = CreateStatus("y"),
        };

        // Act & Assert
        strategy.SelectProvider(providers, health).Should().Be("x");
        strategy.SelectProvider(providers, health).Should().Be("y");
        strategy.SelectProvider(providers, health).Should().Be("x");
        strategy.SelectProvider(providers, health).Should().Be("y");
    }

    [Fact]
    public void SelectProvider_IsThreadSafe_NoConcurrentCorruption()
    {
        // Arrange
        var strategy = new RoundRobinStrategy();
        var providers = new List<string> { "a", "b", "c" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["a"] = CreateStatus("a"),
            ["b"] = CreateStatus("b"),
            ["c"] = CreateStatus("c"),
        };
        var validProviders = new HashSet<string>(providers);
        int totalSelections = 300;
        var results = new string[totalSelections];

        // Act - concurrent selections
        Parallel.For(0, totalSelections, i =>
        {
            results[i] = strategy.SelectProvider(providers, health);
        });

        // Assert - all results must be valid provider IDs
        foreach (var result in results)
        {
            validProviders.Should().Contain(result);
        }

        // Each provider should have been selected approximately equally
        var counts = results.GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());
        counts.Should().HaveCount(3);
        foreach (var kvp in counts)
        {
            kvp.Value.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void SelectProvider_WrapsAroundCorrectly_AfterManyIterations()
    {
        // Arrange
        var strategy = new RoundRobinStrategy();
        var providers = new List<string> { "p1", "p2", "p3" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["p1"] = CreateStatus("p1"),
            ["p2"] = CreateStatus("p2"),
            ["p3"] = CreateStatus("p3"),
        };

        // Act - select many times to verify wrap-around
        var selections = new List<string>();
        for (int i = 0; i < 9; i++)
        {
            selections.Add(strategy.SelectProvider(providers, health));
        }

        // Assert - should repeat the pattern exactly
        selections.Should().Equal("p1", "p2", "p3", "p1", "p2", "p3", "p1", "p2", "p3");
    }

    [Fact]
    public void SelectProvider_ImplementsIProviderSelectionStrategy()
    {
        // Arrange & Act
        var strategy = new RoundRobinStrategy();

        // Assert
        strategy.Should().BeAssignableTo<IProviderSelectionStrategy>();
    }
}

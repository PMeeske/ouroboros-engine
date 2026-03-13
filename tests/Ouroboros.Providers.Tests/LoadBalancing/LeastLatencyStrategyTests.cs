// <copyright file="LeastLatencyStrategyTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Providers.LoadBalancing.Strategies;

namespace Ouroboros.Tests.Providers.LoadBalancing;

/// <summary>
/// Unit tests for <see cref="LeastLatencyStrategy"/>.
/// </summary>
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
        // Arrange
        var strategy = new LeastLatencyStrategy();

        // Act
        var name = strategy.Name;

        // Assert
        name.Should().Be("LeastLatency");
    }

    [Fact]
    public void SelectProvider_ReturnsLowestLatency()
    {
        // Arrange
        var strategy = new LeastLatencyStrategy();
        var providers = new List<string> { "a", "b", "c" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["a"] = CreateStatus("a", averageLatencyMs: 500),
            ["b"] = CreateStatus("b", averageLatencyMs: 100),
            ["c"] = CreateStatus("c", averageLatencyMs: 300),
        };

        // Act
        var selected = strategy.SelectProvider(providers, health);

        // Assert
        selected.Should().Be("b");
    }

    [Fact]
    public void SelectProvider_NullProviders_ThrowsArgumentException()
    {
        // Arrange
        var strategy = new LeastLatencyStrategy();

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
        var strategy = new LeastLatencyStrategy();

        // Act
        Action act = () => strategy.SelectProvider(new List<string>(), new Dictionary<string, ProviderHealthStatus>());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("healthyProviders");
    }

    [Fact]
    public void SelectProvider_SingleProvider_ReturnsThatProvider()
    {
        // Arrange
        var strategy = new LeastLatencyStrategy();
        var providers = new List<string> { "solo" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["solo"] = CreateStatus("solo", averageLatencyMs: 999),
        };

        // Act
        var selected = strategy.SelectProvider(providers, health);

        // Assert
        selected.Should().Be("solo");
    }

    [Fact]
    public void SelectProvider_EqualLatencies_ReturnsFirst()
    {
        // Arrange
        var strategy = new LeastLatencyStrategy();
        var providers = new List<string> { "a", "b", "c" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["a"] = CreateStatus("a", averageLatencyMs: 200),
            ["b"] = CreateStatus("b", averageLatencyMs: 200),
            ["c"] = CreateStatus("c", averageLatencyMs: 200),
        };

        // Act
        var selected = strategy.SelectProvider(providers, health);

        // Assert - OrderBy is stable, so first with minimum wins
        selected.Should().Be("a");
    }

    [Fact]
    public void SelectProvider_VeryLowLatency_SelectsCorrectly()
    {
        // Arrange
        var strategy = new LeastLatencyStrategy();
        var providers = new List<string> { "slow", "fast" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["slow"] = CreateStatus("slow", averageLatencyMs: 5000),
            ["fast"] = CreateStatus("fast", averageLatencyMs: 1),
        };

        // Act
        var selected = strategy.SelectProvider(providers, health);

        // Assert
        selected.Should().Be("fast");
    }

    [Fact]
    public void SelectProvider_ZeroLatency_SelectsCorrectly()
    {
        // Arrange
        var strategy = new LeastLatencyStrategy();
        var providers = new List<string> { "normal", "instant" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["normal"] = CreateStatus("normal", averageLatencyMs: 100),
            ["instant"] = CreateStatus("instant", averageLatencyMs: 0),
        };

        // Act
        var selected = strategy.SelectProvider(providers, health);

        // Assert
        selected.Should().Be("instant");
    }

    [Fact]
    public void SelectProvider_ConsistentlyPicksLowest_AcrossMultipleCalls()
    {
        // Arrange
        var strategy = new LeastLatencyStrategy();
        var providers = new List<string> { "a", "b", "c" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["a"] = CreateStatus("a", averageLatencyMs: 300),
            ["b"] = CreateStatus("b", averageLatencyMs: 50),
            ["c"] = CreateStatus("c", averageLatencyMs: 200),
        };

        // Act & Assert - deterministic: always picks lowest
        for (int i = 0; i < 5; i++)
        {
            strategy.SelectProvider(providers, health).Should().Be("b");
        }
    }

    [Fact]
    public void SelectProvider_ImplementsIProviderSelectionStrategy()
    {
        // Arrange & Act
        var strategy = new LeastLatencyStrategy();

        // Assert
        strategy.Should().BeAssignableTo<IProviderSelectionStrategy>();
    }
}

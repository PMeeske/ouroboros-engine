// <copyright file="AdaptiveHealthStrategyTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Providers.LoadBalancing.Strategies;

namespace Ouroboros.Tests.Providers.LoadBalancing;

/// <summary>
/// Unit tests for <see cref="AdaptiveHealthStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AdaptiveHealthStrategyTests
{
    /// <summary>
    /// Creates a <see cref="ProviderHealthStatus"/> with a controllable HealthScore.
    /// HealthScore = SuccessRate * 0.7 + max(0, 1 - AverageLatencyMs/10000) * 0.3
    /// when IsHealthy=true and not in cooldown.
    /// </summary>
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
    public void Name_ReturnsAdaptiveHealth()
    {
        // Arrange
        var strategy = new AdaptiveHealthStrategy();

        // Act
        var name = strategy.Name;

        // Assert
        name.Should().Be("AdaptiveHealth");
    }

    [Fact]
    public void SelectProvider_ReturnsHighestHealthScore()
    {
        // Arrange
        var strategy = new AdaptiveHealthStrategy();
        var providers = new List<string> { "a", "b", "c" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            // HealthScore(a) = 0.5*0.7 + (1-100/10000)*0.3 = 0.35 + 0.297 = 0.647
            ["a"] = CreateStatus("a", successRate: 0.5, averageLatencyMs: 100),
            // HealthScore(b) = 0.9*0.7 + (1-100/10000)*0.3 = 0.63 + 0.297 = 0.927
            ["b"] = CreateStatus("b", successRate: 0.9, averageLatencyMs: 100),
            // HealthScore(c) = 0.7*0.7 + (1-100/10000)*0.3 = 0.49 + 0.297 = 0.787
            ["c"] = CreateStatus("c", successRate: 0.7, averageLatencyMs: 100),
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
        var strategy = new AdaptiveHealthStrategy();

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
        var strategy = new AdaptiveHealthStrategy();

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
        var strategy = new AdaptiveHealthStrategy();
        var providers = new List<string> { "solo" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["solo"] = CreateStatus("solo", successRate: 0.5),
        };

        // Act
        var selected = strategy.SelectProvider(providers, health);

        // Assert
        selected.Should().Be("solo");
    }

    [Fact]
    public void SelectProvider_HighSuccessRateBeatsLowLatency()
    {
        // Arrange
        var strategy = new AdaptiveHealthStrategy();
        var providers = new List<string> { "fast", "reliable" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            // fast: HealthScore = 0.6*0.7 + (1-10/10000)*0.3 = 0.42 + 0.2997 = 0.7197
            ["fast"] = CreateStatus("fast", successRate: 0.6, averageLatencyMs: 10),
            // reliable: HealthScore = 1.0*0.7 + (1-1000/10000)*0.3 = 0.7 + 0.27 = 0.97
            ["reliable"] = CreateStatus("reliable", successRate: 1.0, averageLatencyMs: 1000),
        };

        // Act
        var selected = strategy.SelectProvider(providers, health);

        // Assert - reliability (success rate) is weighted 70% vs latency 30%
        selected.Should().Be("reliable");
    }

    [Fact]
    public void SelectProvider_EqualScores_ReturnsFirstInList()
    {
        // Arrange
        var strategy = new AdaptiveHealthStrategy();
        var providers = new List<string> { "a", "b" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["a"] = CreateStatus("a", successRate: 0.9, averageLatencyMs: 100),
            ["b"] = CreateStatus("b", successRate: 0.9, averageLatencyMs: 100),
        };

        // Act
        var selected = strategy.SelectProvider(providers, health);

        // Assert - OrderByDescending is stable, first with max wins
        selected.Should().Be("a");
    }

    [Fact]
    public void SelectProvider_ConsistentlyPicksHighestScore_AcrossMultipleCalls()
    {
        // Arrange
        var strategy = new AdaptiveHealthStrategy();
        var providers = new List<string> { "low", "high", "mid" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            ["low"] = CreateStatus("low", successRate: 0.3),
            ["high"] = CreateStatus("high", successRate: 0.99),
            ["mid"] = CreateStatus("mid", successRate: 0.7),
        };

        // Act & Assert - deterministic: always picks highest health score
        for (int i = 0; i < 5; i++)
        {
            strategy.SelectProvider(providers, health).Should().Be("high");
        }
    }

    [Fact]
    public void SelectProvider_VeryHighLatencyReducesScore()
    {
        // Arrange
        var strategy = new AdaptiveHealthStrategy();
        var providers = new List<string> { "laggy", "normal" };
        var health = new Dictionary<string, ProviderHealthStatus>
        {
            // laggy: HealthScore = 0.95*0.7 + max(0, 1-15000/10000)*0.3 = 0.665 + 0 = 0.665
            ["laggy"] = CreateStatus("laggy", successRate: 0.95, averageLatencyMs: 15000),
            // normal: HealthScore = 0.85*0.7 + (1-200/10000)*0.3 = 0.595 + 0.294 = 0.889
            ["normal"] = CreateStatus("normal", successRate: 0.85, averageLatencyMs: 200),
        };

        // Act
        var selected = strategy.SelectProvider(providers, health);

        // Assert - normal wins because laggy's latency score is clamped to 0
        selected.Should().Be("normal");
    }

    [Fact]
    public void SelectProvider_ImplementsIProviderSelectionStrategy()
    {
        // Arrange & Act
        var strategy = new AdaptiveHealthStrategy();

        // Assert
        strategy.Should().BeAssignableTo<IProviderSelectionStrategy>();
    }
}

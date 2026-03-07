// <copyright file="ProviderSelectionStrategiesTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Providers.LoadBalancing.Strategies;

namespace Ouroboros.Tests.Providers.LoadBalancing;

/// <summary>
/// Unit tests for <see cref="ProviderSelectionStrategies"/> static factory.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProviderSelectionStrategiesTests
{
    [Fact]
    public void RoundRobin_ReturnsRoundRobinStrategy()
    {
        // Arrange & Act
        var strategy = ProviderSelectionStrategies.RoundRobin;

        // Assert
        strategy.Should().BeOfType<RoundRobinStrategy>();
        strategy.Name.Should().Be("RoundRobin");
    }

    [Fact]
    public void LeastLatency_ReturnsLeastLatencyStrategy()
    {
        // Arrange & Act
        var strategy = ProviderSelectionStrategies.LeastLatency;

        // Assert
        strategy.Should().BeOfType<LeastLatencyStrategy>();
        strategy.Name.Should().Be("LeastLatency");
    }

    [Fact]
    public void AdaptiveHealth_ReturnsAdaptiveHealthStrategy()
    {
        // Arrange & Act
        var strategy = ProviderSelectionStrategies.AdaptiveHealth;

        // Assert
        strategy.Should().BeOfType<AdaptiveHealthStrategy>();
        strategy.Name.Should().Be("AdaptiveHealth");
    }

    [Fact]
    public void WeightedRandom_ReturnsWeightedRandomStrategy()
    {
        // Arrange & Act
        var strategy = ProviderSelectionStrategies.WeightedRandom;

        // Assert
        strategy.Should().BeOfType<WeightedRandomStrategy>();
        strategy.Name.Should().Be("WeightedRandom");
    }

    [Fact]
    public void RoundRobin_EachCall_ReturnsNewInstance()
    {
        // Arrange & Act
        var a = ProviderSelectionStrategies.RoundRobin;
        var b = ProviderSelectionStrategies.RoundRobin;

        // Assert
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void LeastLatency_EachCall_ReturnsNewInstance()
    {
        // Arrange & Act
        var a = ProviderSelectionStrategies.LeastLatency;
        var b = ProviderSelectionStrategies.LeastLatency;

        // Assert
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void AdaptiveHealth_EachCall_ReturnsNewInstance()
    {
        // Arrange & Act
        var a = ProviderSelectionStrategies.AdaptiveHealth;
        var b = ProviderSelectionStrategies.AdaptiveHealth;

        // Assert
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void WeightedRandom_EachCall_ReturnsNewInstance()
    {
        // Arrange & Act
        var a = ProviderSelectionStrategies.WeightedRandom;
        var b = ProviderSelectionStrategies.WeightedRandom;

        // Assert
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void AllStrategies_ImplementIProviderSelectionStrategy()
    {
        // Arrange & Act & Assert
        ProviderSelectionStrategies.RoundRobin.Should().BeAssignableTo<IProviderSelectionStrategy>();
        ProviderSelectionStrategies.LeastLatency.Should().BeAssignableTo<IProviderSelectionStrategy>();
        ProviderSelectionStrategies.AdaptiveHealth.Should().BeAssignableTo<IProviderSelectionStrategy>();
        ProviderSelectionStrategies.WeightedRandom.Should().BeAssignableTo<IProviderSelectionStrategy>();
    }
}

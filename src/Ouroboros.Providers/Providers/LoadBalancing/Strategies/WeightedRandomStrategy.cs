// <copyright file="WeightedRandomStrategy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Security.Cryptography;

namespace Ouroboros.Providers.LoadBalancing.Strategies;

/// <summary>
/// Weighted random strategy that probabilistically selects providers based on their health scores.
/// Providers with higher health scores are more likely to be selected.
/// </summary>
public sealed class WeightedRandomStrategy : IProviderSelectionStrategy
{
    /// <inheritdoc/>
    public string Name => "WeightedRandom";

    /// <inheritdoc/>
    public string SelectProvider(List<string> healthyProviders, IReadOnlyDictionary<string, ProviderHealthStatus> healthStatus)
    {
        if (healthyProviders == null || healthyProviders.Count == 0)
            throw new ArgumentException("No healthy providers available", nameof(healthyProviders));

        // Weight by health score
        var weights = healthyProviders
            .Select(id => (healthStatus[id].HealthScore, id))
            .ToList();

        double totalWeight = weights.Sum(w => w.Item1);
        if (totalWeight <= 0)
            return healthyProviders[RandomNumberGenerator.GetInt32(healthyProviders.Count)];

        double randomValue = RandomNumberGenerator.GetInt32(256) * totalWeight;
        double cumulative = 0;

        foreach (var (weight, id) in weights)
        {
            cumulative += weight;
            if (randomValue <= cumulative)
                return id;
        }

        return healthyProviders.Last();
    }
}

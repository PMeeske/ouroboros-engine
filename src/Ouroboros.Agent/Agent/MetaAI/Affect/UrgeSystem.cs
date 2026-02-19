// <copyright file="UrgeSystem.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ==========================================================
// Urge/Drive System — Psi-theory MicroPsi integration
// Drives compete for attention based on urgency (intensity × priority).
// ==========================================================

namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// Represents a Psi-theory drive/urge that accumulates over time.
/// Drives compete for the agent's attention based on urgency.
/// </summary>
public sealed record Urge(
    string Name,
    string Description,
    double Intensity,
    double SatisfactionRate,
    double AccumulationRate,
    double Priority);

/// <summary>
/// Interface for the Psi-theory urge/drive system.
/// Urges accumulate per cognitive cycle and compete for the agent's attention.
/// </summary>
public interface IUrgeSystem
{
    /// <summary>
    /// Gets all current urges with their intensities.
    /// </summary>
    IReadOnlyList<Urge> Urges { get; }

    /// <summary>
    /// Gets the most urgent drive (highest intensity × priority).
    /// </summary>
    Urge? GetDominantUrge();

    /// <summary>
    /// Advances all urges by one cycle — intensities increase by their accumulation rates.
    /// </summary>
    void Tick();

    /// <summary>
    /// Records that a specific urge was satisfied.
    /// Reduces intensity by satisfactionRate × amount.
    /// </summary>
    /// <param name="urgeName">Name of the urge to satisfy.</param>
    /// <param name="amount">Satisfaction amount (0.0 to 1.0).</param>
    void Satisfy(string urgeName, double amount = 1.0);

    /// <summary>
    /// Projects all urges into MeTTa representation.
    /// </summary>
    /// <param name="instanceId">The Ouroboros instance identifier.</param>
    /// <returns>MeTTa-formatted string of urge atoms.</returns>
    string ToMeTTa(string instanceId);
}

/// <summary>
/// Implementation of the Psi-theory urge/drive system.
/// Default urges map MicroPsi drives to AI agent needs:
/// competence, certainty, affiliation, curiosity, integrity.
/// </summary>
public sealed class UrgeSystem : IUrgeSystem
{
    private readonly List<Urge> _urges;
    private readonly double _stress;

    /// <summary>
    /// Initializes a new instance of the <see cref="UrgeSystem"/> class with default drives.
    /// </summary>
    /// <param name="stress">Current stress level for urgency amplification.</param>
    public UrgeSystem(double stress = 0.0)
    {
        _stress = Math.Clamp(stress, 0.0, 1.0);
        _urges = new List<Urge>
        {
            new("competence", "Need to successfully complete goals", 0.3, 0.4, 0.05, 1.0),
            new("certainty", "Need for verified, certain outcomes", 0.3, 0.5, 0.04, 0.9),
            new("affiliation", "Need for human interaction and feedback", 0.2, 0.3, 0.03, 0.7),
            new("curiosity", "Need to explore novel domains and information", 0.4, 0.35, 0.06, 0.8),
            new("integrity", "Need for clean execution without errors", 0.2, 0.45, 0.03, 0.95),
        };
    }

    /// <inheritdoc/>
    public IReadOnlyList<Urge> Urges => _urges.AsReadOnly();

    /// <inheritdoc/>
    public Urge? GetDominantUrge()
    {
        if (_urges.Count == 0)
        {
            return null;
        }

        return _urges.MaxBy(u => u.Intensity * u.Priority * (1.0 + (_stress * 0.3)));
    }

    /// <inheritdoc/>
    public void Tick()
    {
        for (int i = 0; i < _urges.Count; i++)
        {
            Urge u = _urges[i];
            double newIntensity = Math.Clamp(u.Intensity + u.AccumulationRate, 0.0, 1.0);
            _urges[i] = u with { Intensity = newIntensity };
        }
    }

    /// <inheritdoc/>
    public void Satisfy(string urgeName, double amount = 1.0)
    {
        ArgumentNullException.ThrowIfNull(urgeName);
        amount = Math.Clamp(amount, 0.0, 1.0);

        for (int i = 0; i < _urges.Count; i++)
        {
            Urge u = _urges[i];
            if (string.Equals(u.Name, urgeName, StringComparison.OrdinalIgnoreCase))
            {
                double reduction = u.SatisfactionRate * amount;
                double newIntensity = Math.Clamp(u.Intensity - reduction, 0.0, 1.0);
                _urges[i] = u with { Intensity = newIntensity };
                return;
            }
        }
    }

    /// <inheritdoc/>
    public string ToMeTTa(string instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        StringBuilder sb = new();

        foreach (Urge u in _urges)
        {
            sb.AppendLine($"(HasUrge (OuroborosInstance \"{instanceId}\") (Urge \"{u.Name}\" {u.Intensity:F2}))");
        }

        Urge? dominant = GetDominantUrge();
        if (dominant != null)
        {
            sb.AppendLine($"(DominantUrge (OuroborosInstance \"{instanceId}\") \"{dominant.Name}\")");
        }

        return sb.ToString();
    }
}

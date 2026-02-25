// <copyright file="SpreadingActivation.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ==========================================================
// Spreading Activation — MicroPsi associative memory
// Activation spreads through AtomSpace connections, decaying
// with distance. High arousal narrows spread (focused attention).
// ==========================================================

using Ouroboros.Core.Hyperon;

namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// Implements spreading activation over the AtomSpace.
/// When an atom is activated (by perception, goal-setting, or experience recall),
/// activation spreads to connected atoms, decaying with distance.
/// </summary>
public sealed class SpreadingActivation
{
    private readonly IAtomSpace _space;
    private readonly ConcurrentDictionary<string, double> _activationLevels = new();
    private readonly double _decayRate;
    private readonly double _activationThreshold;
    private double _spreadFactor;
    private int _effectiveMaxDepth;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpreadingActivation"/> class.
    /// </summary>
    /// <param name="space">The atom space to spread activation over.</param>
    /// <param name="decayRate">Rate at which activations decay per time step (0.0–1.0).</param>
    /// <param name="spreadFactor">Factor controlling how much energy spreads to neighbors (0.0–1.0).</param>
    /// <param name="activationThreshold">Minimum activation level to keep an atom active.</param>
    public SpreadingActivation(
        IAtomSpace space,
        double decayRate = 0.15,
        double spreadFactor = 0.5,
        double activationThreshold = 0.1)
    {
        _space = space ?? throw new ArgumentNullException(nameof(space));
        _decayRate = decayRate;
        _spreadFactor = spreadFactor;
        _activationThreshold = activationThreshold;
        _effectiveMaxDepth = 3;
    }

    /// <summary>
    /// Activates an atom and spreads activation to connected atoms.
    /// </summary>
    /// <param name="atomKey">The S-expression key of the atom to activate.</param>
    /// <param name="energy">Initial activation energy (0.0–1.0).</param>
    public void Activate(string atomKey, double energy = 1.0)
    {
        ArgumentNullException.ThrowIfNull(atomKey);
        _activationLevels.AddOrUpdate(atomKey, energy, (_, old) => Math.Min(old + energy, 1.0));
        Spread(atomKey, energy * _spreadFactor, depth: 0);
    }

    /// <summary>
    /// Decays all activations by one time step.
    /// </summary>
    public void Decay()
    {
        foreach (string key in _activationLevels.Keys.ToList())
        {
            _activationLevels.AddOrUpdate(key, 0, (_, old) => old * (1.0 - _decayRate));
            if (_activationLevels.TryGetValue(key, out double val) && val < _activationThreshold)
            {
                _activationLevels.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Gets atoms above activation threshold, ordered by activation level.
    /// These are the "primed" concepts available for planning and reasoning.
    /// </summary>
    /// <returns>List of (atomKey, activation) pairs ordered by activation descending.</returns>
    public IReadOnlyList<(string AtomKey, double Activation)> GetActivatedAtoms()
    {
        return _activationLevels
            .Where(kv => kv.Value >= _activationThreshold)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }

    /// <summary>
    /// Modulates spread factor by arousal.
    /// High arousal → narrower spread (focused attention).
    /// Low arousal → wider spread (diffuse, creative associations).
    /// </summary>
    /// <param name="arousal">Current arousal level (0.0–1.0).</param>
    /// <param name="baseSpreadFactor">Original base spread factor.</param>
    public void ModulateByArousal(double arousal, double baseSpreadFactor = 0.5)
    {
        _spreadFactor = baseSpreadFactor * (1.0 - (arousal * 0.5));
        _effectiveMaxDepth = arousal > 0.7 ? 2 : 3;
    }

    private void Spread(string fromKey, double energy, int depth)
    {
        if (depth >= _effectiveMaxDepth || energy < _activationThreshold)
        {
            return;
        }

        IEnumerable<string> connected = _space.All()
            .OfType<Expression>()
            .Where(e => e.Children.Any(c => c.ToSExpr() == fromKey))
            .SelectMany(e => e.Children)
            .Select(c => c.ToSExpr())
            .Where(k => k != fromKey)
            .Distinct();

        foreach (string neighbor in connected)
        {
            _activationLevels.AddOrUpdate(neighbor, energy, (_, old) => Math.Min(old + energy, 1.0));
            Spread(neighbor, energy * _spreadFactor, depth + 1);
        }
    }
}

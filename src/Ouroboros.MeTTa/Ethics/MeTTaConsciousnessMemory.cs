// <copyright file="MeTTaConsciousnessMemory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.MeTTa.Ethics.Loaders;

namespace Ouroboros.MeTTa.Ethics;

/// <summary>
/// IIT-flavored consciousness memory layer that wraps an
/// <see cref="IAtomSpace"/> and tracks a phi-proxy reflecting the
/// integration of ethical atoms across traditions.
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally a thin wrapper, not a full Tononi
/// integrated-information implementation. The phi-proxy is computed
/// as the ratio of distinct traditions present to the total enum size,
/// scaled by an atom-density factor. It is monotone in tradition
/// coverage and atom richness.
/// </para>
/// <para>
/// The class also exposes a per-tradition trace count so the
/// <see cref="EthicsConflictResolver"/> can dampen its confidence
/// when integration is poor.
/// </para>
/// </remarks>
public sealed class MeTTaConsciousnessMemory
{
    private readonly IAtomSpace _space;
    private readonly Dictionary<EthicsTradition, int> _atomsByTradition = new();

    /// <summary>
    /// Initializes a new instance over an existing atom space.
    /// </summary>
    /// <param name="space">The backing atom space.</param>
    public MeTTaConsciousnessMemory(IAtomSpace space)
    {
        ArgumentNullException.ThrowIfNull(space);
        _space = space;
    }

    /// <summary>
    /// Records that a tradition contributed atoms to the wrapped space.
    /// </summary>
    /// <param name="tradition">The tradition.</param>
    /// <param name="atomsLoaded">How many atoms were inserted.</param>
    public void RecordTradition(EthicsTradition tradition, int atomsLoaded)
    {
        if (atomsLoaded <= 0)
        {
            return;
        }

        if (_atomsByTradition.TryGetValue(tradition, out int existing))
        {
            _atomsByTradition[tradition] = existing + atomsLoaded;
        }
        else
        {
            _atomsByTradition[tradition] = atomsLoaded;
        }
    }

    /// <summary>
    /// Imports an entire <see cref="EthicsAtomLoadReport"/> into the trace.
    /// </summary>
    /// <param name="report">The report to record.</param>
    public void ImportReport(EthicsAtomLoadReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        foreach (EthicsLoadResult r in report.Loaded)
        {
            RecordTradition(r.Tradition, r.AtomsLoaded);
        }
    }

    /// <summary>
    /// Gets the count of atoms recorded for a tradition.
    /// </summary>
    /// <param name="tradition">The tradition.</param>
    /// <returns>Atom count, or 0 if the tradition has not contributed.</returns>
    public int AtomsFor(EthicsTradition tradition)
    {
        return _atomsByTradition.TryGetValue(tradition, out int count) ? count : 0;
    }

    /// <summary>
    /// Gets the count of distinct traditions that have contributed atoms.
    /// </summary>
    public int DistinctTraditionsLoaded => _atomsByTradition.Keys.Count;

    /// <summary>
    /// Gets the total atom count across all traditions.
    /// </summary>
    public int TotalEthicsAtoms => _atomsByTradition.Values.Sum();

    /// <summary>
    /// Gets the underlying atom-space size (may include non-ethics atoms).
    /// </summary>
    public int AtomSpaceSize => _space.Count;

    /// <summary>
    /// Computes the phi-proxy for the current trace in 0..1.
    /// </summary>
    /// <returns>An IIT-flavored integration estimate.</returns>
    public double ComputePhiProxy()
    {
        int totalTraditions = Enum.GetValues<EthicsTradition>().Length;
        if (totalTraditions == 0)
        {
            return 0.0;
        }

        double coverage = (double)DistinctTraditionsLoaded / totalTraditions;

        // Density factor: average atoms per loaded tradition, normalized to a
        // small saturation point (32 atoms per tradition is plenty).
        double density = DistinctTraditionsLoaded == 0
            ? 0.0
            : Math.Min(1.0, (double)TotalEthicsAtoms / (DistinctTraditionsLoaded * 32.0));

        return Math.Clamp(coverage * density, 0.0, 1.0);
    }
}

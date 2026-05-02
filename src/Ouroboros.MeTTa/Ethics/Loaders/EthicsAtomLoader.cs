// <copyright file="EthicsAtomLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics.Loaders;

/// <summary>
/// IIT-aware aggregate loader — runs every tradition-specific loader and
/// computes a phi-proxy from the count of distinct atoms ingested.
/// This is the engine-layer companion to the foundation
/// <c>Ouroboros.Core.Ethics.MeTTa.EthicsAtomLoader</c>; it operates over
/// the engine's own embedded MeTTa resources and an in-memory atom space
/// rather than the foundation's enum-driven hash-verified pipeline.
/// </summary>
public sealed class EthicsAtomLoader
{
    private readonly IReadOnlyList<IEthicsLoader> _loaders;

    /// <summary>
    /// Initializes a new instance backed by the standard 9 tradition loaders.
    /// </summary>
    public EthicsAtomLoader()
        : this(StandardLoaders())
    {
    }

    /// <summary>
    /// Initializes a new instance backed by an explicit loader set
    /// (useful for tests or selective loading).
    /// </summary>
    /// <param name="loaders">Loaders to invoke during <see cref="LoadAll"/>.</param>
    public EthicsAtomLoader(IEnumerable<IEthicsLoader> loaders)
    {
        ArgumentNullException.ThrowIfNull(loaders);
        _loaders = loaders.ToImmutableArray();
    }

    /// <summary>
    /// Loads every registered tradition into the given atom space and
    /// returns aggregated metadata.
    /// </summary>
    /// <param name="space">The target atom space.</param>
    /// <returns>Aggregated load report.</returns>
    public EthicsAtomLoadReport LoadAll(IAtomSpace space)
    {
        ArgumentNullException.ThrowIfNull(space);

        List<EthicsLoadResult> successes = new();
        List<(EthicsTradition Tradition, string Reason)> failures = new();

        foreach (IEthicsLoader loader in _loaders)
        {
            Result<EthicsLoadResult, string> r = loader.Load(space);
            if (r.IsSuccess)
            {
                successes.Add(r.Value);
            }
            else
            {
                failures.Add((loader.Tradition, r.Error));
            }
        }

        // Phi-proxy: a Tonioni-flavored stand-in for integrated information.
        // The more independent traditions successfully integrate, the higher the proxy.
        int distinctSources = successes.Select(s => s.Tradition).Distinct().Count();
        double phiProxy = _loaders.Count == 0
            ? 0.0
            : (double)distinctSources / _loaders.Count;

        return new EthicsAtomLoadReport(
            Loaded: successes,
            Failed: failures,
            PhiProxy: phiProxy,
            TotalAtoms: successes.Sum(s => s.AtomsLoaded));
    }

    private static IEnumerable<IEthicsLoader> StandardLoaders() => new IEthicsLoader[]
    {
        new CoreEthicsLoader(),
        new AhimsaLoader(),
        new BhagavadGitaLoader(),
        new KantianLoader(),
        new LevinasLoader(),
        new MadhyamakaLoader(),
        new UbuntuLoader(),
        new WisdomOfDisagreementLoader(),
        new ParadoxHandler(),
    };
}

/// <summary>
/// Aggregate report from a full ethics load pass.
/// </summary>
/// <param name="Loaded">Successfully loaded traditions and their per-file metadata.</param>
/// <param name="Failed">Failed tradition loads with reasons.</param>
/// <param name="PhiProxy">A 0..1 stand-in for integrated information across traditions.</param>
/// <param name="TotalAtoms">Total atoms inserted across all traditions.</param>
public sealed record EthicsAtomLoadReport(
    IReadOnlyList<EthicsLoadResult> Loaded,
    IReadOnlyList<(EthicsTradition Tradition, string Reason)> Failed,
    double PhiProxy,
    int TotalAtoms);

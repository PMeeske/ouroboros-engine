// <copyright file="MadhyamakaLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics.Loaders;

/// <summary>
/// Loads <c>nagarjuna.metta</c> — Madhyamaka emptiness.
/// </summary>
public sealed class MadhyamakaLoader : EthicsLoaderBase
{
    /// <inheritdoc/>
    public override EthicsTradition Tradition => EthicsTradition.Nagarjuna;

    /// <inheritdoc/>
    protected override string ResourceName => "Ouroboros.MeTTa.Ethics.nagarjuna.metta";

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Fingerprints => new[]
    {
        "dependently-co-arisen",
        "tetralemma",
        "conventional-designation",
    };
}

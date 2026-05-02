// <copyright file="AhimsaLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics.Loaders;

/// <summary>
/// Loads <c>ahimsa.metta</c> — non-harm ethics (Jain, Gandhi).
/// </summary>
public sealed class AhimsaLoader : EthicsLoaderBase
{
    /// <inheritdoc/>
    public override EthicsTradition Tradition => EthicsTradition.Ahimsa;

    /// <inheritdoc/>
    protected override string ResourceName => "Ouroboros.MeTTa.Ethics.ahimsa.metta";

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Fingerprints => new[]
    {
        "cultivated-ignorance",
        "perfect-nonharm",
        "ahimsa",
    };
}

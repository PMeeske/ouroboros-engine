// <copyright file="CoreEthicsLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics.Loaders;

/// <summary>
/// Loads <c>core_ethics.metta</c> — relational/care ethics atoms (Gilligan, Noddings, Held).
/// </summary>
public sealed class CoreEthicsLoader : EthicsLoaderBase
{
    /// <inheritdoc/>
    public override EthicsTradition Tradition => EthicsTradition.CoreEthics;

    /// <inheritdoc/>
    protected override string ResourceName => "Ouroboros.MeTTa.Ethics.core_ethics.metta";

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Fingerprints => new[]
    {
        "not-separable harm care",
        "care-for",
        "relational-harmony",
    };
}

// <copyright file="UbuntuLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics.Loaders;

/// <summary>
/// Loads <c>ubuntu.metta</c> — communal personhood.
/// </summary>
public sealed class UbuntuLoader : EthicsLoaderBase
{
    /// <inheritdoc/>
    public override EthicsTradition Tradition => EthicsTradition.Ubuntu;

    /// <inheritdoc/>
    protected override string ResourceName => "Ouroboros.MeTTa.Ethics.ubuntu.metta";

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Fingerprints => new[]
    {
        "i-am we-are",
        "atomistic-individual",
        "flourishing",
    };
}

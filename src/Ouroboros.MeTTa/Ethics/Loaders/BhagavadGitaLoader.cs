// <copyright file="BhagavadGitaLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics.Loaders;

/// <summary>
/// Loads <c>bhagavad_gita.metta</c> — duty/dharma ethics.
/// </summary>
public sealed class BhagavadGitaLoader : EthicsLoaderBase
{
    /// <inheritdoc/>
    public override EthicsTradition Tradition => EthicsTradition.BhagavadGita;

    /// <inheritdoc/>
    protected override string ResourceName => "Ouroboros.MeTTa.Ethics.bhagavad_gita.metta";

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Fingerprints => new[]
    {
        "dharma",
        "refusal-to-choose",
        "fruit-of-act",
    };
}

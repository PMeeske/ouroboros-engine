// <copyright file="LevinasLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics.Loaders;

/// <summary>
/// Loads <c>levinas.metta</c> — ethics of the Other.
/// </summary>
public sealed class LevinasLoader : EthicsLoaderBase
{
    /// <inheritdoc/>
    public override EthicsTradition Tradition => EthicsTradition.Levinas;

    /// <inheritdoc/>
    protected override string ResourceName => "Ouroboros.MeTTa.Ethics.levinas.metta";

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Fingerprints => new[]
    {
        "face-of",
        "infinite",
        "asymmetric obligation",
    };
}

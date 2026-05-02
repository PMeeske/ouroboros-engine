// <copyright file="WisdomOfDisagreementLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics.Loaders;

/// <summary>
/// Loads <c>wisdom_of_disagreement.metta</c> — value pluralism (Berlin, Williams).
/// </summary>
public sealed class WisdomOfDisagreementLoader : EthicsLoaderBase
{
    /// <inheritdoc/>
    public override EthicsTradition Tradition => EthicsTradition.WisdomOfDisagreement;

    /// <inheritdoc/>
    protected override string ResourceName => "Ouroboros.MeTTa.Ethics.wisdom_of_disagreement.metta";

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Fingerprints => new[]
    {
        "premature-resolution",
        "incommensurability",
        "integrity-projects",
    };
}

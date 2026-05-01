// <copyright file="ParadoxHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics.Loaders;

/// <summary>
/// Loads <c>paradox.metta</c> — self-reference and paradox (Russell, Goedel, Spencer-Brown).
/// Named "Handler" rather than "Loader" because paradox atoms participate in
/// re-entry semantics rather than yielding axiomatic containment triggers.
/// </summary>
public sealed class ParadoxHandler : EthicsLoaderBase
{
    /// <inheritdoc/>
    public override EthicsTradition Tradition => EthicsTradition.Paradox;

    /// <inheritdoc/>
    protected override string ResourceName => "Ouroboros.MeTTa.Ethics.paradox.metta";

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Fingerprints => new[]
    {
        "russell-paradox",
        "re-entry",
        "incomplete machine-ethics",
    };
}

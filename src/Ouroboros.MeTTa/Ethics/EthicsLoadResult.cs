// <copyright file="EthicsLoadResult.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics;

/// <summary>
/// The outcome of a single ethics tradition load: which tradition,
/// how many atoms were ingested, and whether the file's expected
/// fingerprint atoms were present.
/// </summary>
/// <param name="Tradition">The tradition that was loaded.</param>
/// <param name="AtomsLoaded">Count of atoms inserted into the atom space.</param>
/// <param name="FingerprintsMatched">Whether tradition-specific sentinel atoms were found.</param>
/// <param name="ResourceName">The embedded resource name that was read.</param>
public sealed record EthicsLoadResult(
    EthicsTradition Tradition,
    int AtomsLoaded,
    bool FingerprintsMatched,
    string ResourceName);

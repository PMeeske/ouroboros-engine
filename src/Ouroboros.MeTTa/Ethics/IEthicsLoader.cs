// <copyright file="IEthicsLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics;

/// <summary>
/// Contract for an engine-layer ethics loader: parses one MeTTa tradition
/// file into <see cref="Atom"/> records and inserts them into an
/// <see cref="IAtomSpace"/>.
/// </summary>
public interface IEthicsLoader
{
    /// <summary>
    /// Gets the tradition this loader handles.
    /// </summary>
    EthicsTradition Tradition { get; }

    /// <summary>
    /// Loads the tradition's atoms into the given atom space.
    /// </summary>
    /// <param name="space">The target atom space.</param>
    /// <returns>A success result with load metadata, or a failure with an explanation.</returns>
    Result<EthicsLoadResult, string> Load(IAtomSpace space);
}

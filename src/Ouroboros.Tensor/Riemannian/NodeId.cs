// <copyright file="NodeId.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Riemannian;

/// <summary>
/// A typed identifier for nodes in a <see cref="LocalNeighborhoodGraph"/>.
/// Wraps a string so call sites do not silently swap raw IDs with arbitrary
/// strings.
/// </summary>
/// <param name="Value">The underlying node identifier.</param>
public readonly record struct NodeId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => this.Value;

    /// <summary>
    /// Implicit conversion from a raw string for ergonomic call sites.
    /// </summary>
    /// <param name="value">The raw id.</param>
    public static implicit operator NodeId(string value) => new(value);
}

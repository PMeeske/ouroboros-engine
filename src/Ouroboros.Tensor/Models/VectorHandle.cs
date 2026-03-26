// <copyright file="VectorHandle.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Models;

/// <summary>
/// A lightweight, allocation-free reference to a vector stored in an external vector database.
/// A <see cref="VectorHandle"/> represents <em>location</em>, not data. The actual float array is
/// fetched on demand via <see cref="IHandleAwareVectorStore"/> (R05, R07).
/// </summary>
/// <param name="ProviderId">
/// Identifies the vector store provider (e.g. "qdrant", "pgvector").
/// </param>
/// <param name="CollectionName">The collection or table that holds the vector.</param>
/// <param name="VectorId">The unique identifier of the vector within the collection.</param>
/// <param name="Dimension">
/// The expected dimensionality of the stored vector. Used for pre-allocation and validation.
/// </param>
/// <remarks>
/// Declared as a <see langword="readonly record struct"/> so that equality is structural and
/// instances live on the stack — no heap allocation when used in enumerations (R17).
/// </remarks>
public readonly record struct VectorHandle(
    string ProviderId,
    string CollectionName,
    string VectorId,
    int Dimension)
{
    /// <summary>
    /// Validates that the handle refers to a well-formed vector location.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when any string field is null/empty, or <see cref="Dimension"/> is not positive.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProviderId))
            throw new ArgumentException("ProviderId must not be empty.", nameof(ProviderId));
        if (string.IsNullOrWhiteSpace(CollectionName))
            throw new ArgumentException("CollectionName must not be empty.", nameof(CollectionName));
        if (string.IsNullOrWhiteSpace(VectorId))
            throw new ArgumentException("VectorId must not be empty.", nameof(VectorId));
        if (Dimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(Dimension),
                $"Dimension must be positive. Got {Dimension}.");
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"{ProviderId}/{CollectionName}/{VectorId}[{Dimension}]";
}

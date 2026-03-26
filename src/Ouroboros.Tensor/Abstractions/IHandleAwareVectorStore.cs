// <copyright file="IHandleAwareVectorStore.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// A companion to the domain <c>IVectorStore</c> that operates on lightweight
/// <see cref="VectorHandle"/> references instead of materialised vector data.
/// Implementations may target Qdrant, PostgreSQL pgvector, or any future provider (R06, R16).
/// </summary>
/// <remarks>
/// This interface is intentionally separate from the existing <c>IVectorStore</c> domain type to
/// avoid widening the existing contract (R16). Adapters can bridge the two when needed.
/// </remarks>
public interface IHandleAwareVectorStore
{
    /// <summary>
    /// Fetches the raw vector data identified by <paramref name="handle"/>.
    /// </summary>
    /// <param name="handle">The lightweight reference to the vector in the store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{TSuccess,TError}.Success"/> with the float array on success,
    /// or <see cref="Result{TSuccess,TError}.Failure"/> with an error message.
    /// </returns>
    Task<Result<float[], string>> FetchAsync(
        VectorHandle handle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches multiple vectors in a single round-trip where the provider supports it.
    /// Handles from different providers or collections are batched independently.
    /// </summary>
    /// <param name="handles">Handles to retrieve.</param>
    /// <param name="batchSize">Maximum handles per network request (default 64).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An ordered list of (handle, vector) pairs, or a failure result.
    /// </returns>
    Task<Result<IReadOnlyList<(VectorHandle Handle, float[] Vector)>, string>> FetchBatchAsync(
        IEnumerable<VectorHandle> handles,
        int batchSize = 64,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs similarity search and returns handles to the nearest vectors without
    /// materialising their data — callers decide whether to load them (R07).
    /// </summary>
    /// <param name="queryVector">Query embedding.</param>
    /// <param name="collectionName">Collection to search within.</param>
    /// <param name="topK">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Handles ordered by descending similarity, or a failure result.</returns>
    Task<Result<IReadOnlyList<VectorHandle>, string>> SearchAsync(
        float[] queryVector,
        string collectionName,
        int topK,
        CancellationToken cancellationToken = default);
}

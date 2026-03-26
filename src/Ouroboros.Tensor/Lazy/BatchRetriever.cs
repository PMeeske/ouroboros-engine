// <copyright file="BatchRetriever.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Lazy;

/// <summary>
/// Retrieves vectors from an <see cref="IHandleAwareVectorStore"/> in batches, grouping handles
/// by provider and collection to minimise network round-trips (R08).
/// </summary>
/// <remarks>
/// <para>
/// Handles from the same provider and collection are batched together. Cross-provider handles
/// are issued as separate batch requests. The caller controls batch size via
/// <see cref="DefaultBatchSize"/> or the per-call override on <see cref="FetchAsync"/>.
/// </para>
/// <para>
/// Order is preserved: results are returned in the same order as the input handles.
/// </para>
/// </remarks>
public sealed class BatchRetriever
{
    /// <summary>Default number of handles per batch request.</summary>
    public const int DefaultBatchSize = 64;

    private readonly IHandleAwareVectorStore _store;
    private readonly int _batchSize;

    /// <summary>
    /// Initializes a new <see cref="BatchRetriever"/>.
    /// </summary>
    /// <param name="store">The vector store to retrieve from.</param>
    /// <param name="batchSize">Maximum handles per network call. Defaults to <see cref="DefaultBatchSize"/>.</param>
    public BatchRetriever(IHandleAwareVectorStore store, int batchSize = DefaultBatchSize)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");

        _store = store;
        _batchSize = batchSize;
    }

    /// <summary>
    /// Fetches all vectors identified by <paramref name="handles"/> in batches,
    /// preserving the original ordering of handles in the returned list (R08).
    /// </summary>
    /// <param name="handles">The vector handles to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An ordered list of (handle, vector) pairs on success, or a failure with the first error
    /// encountered.
    /// </returns>
    public async Task<Result<IReadOnlyList<(VectorHandle Handle, float[] Vector)>, string>> FetchAsync(
        IEnumerable<VectorHandle> handles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handles);

        var handleList = handles.ToList();
        if (handleList.Count == 0)
            return Result<IReadOnlyList<(VectorHandle, float[])>, string>.Success(
                Array.Empty<(VectorHandle, float[])>());

        // Preserve original ordering: map index → (handle, result slot)
        var results = new (VectorHandle Handle, float[] Vector)?[handleList.Count];

        // Build index map for result placement
        var indexMap = new Dictionary<string, int>(handleList.Count);
        for (var i = 0; i < handleList.Count; i++)
            indexMap[HandleKey(handleList[i])] = i;

        // Group by provider + collection for efficient batching
        var groups = handleList
            .Select((h, i) => (Handle: h, Index: i))
            .GroupBy(x => (x.Handle.ProviderId, x.Handle.CollectionName));

        foreach (var group in groups)
        {
            var groupHandles = group.Select(x => x.Handle).ToList();

            // Chunk within the group to respect batch size
            for (var offset = 0; offset < groupHandles.Count; offset += _batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var chunk = groupHandles.Skip(offset).Take(_batchSize);
                var batchResult = await _store
                    .FetchBatchAsync(chunk, _batchSize, cancellationToken)
                    .ConfigureAwait(false);

                if (!batchResult.IsSuccess)
                    return Result<IReadOnlyList<(VectorHandle, float[])>, string>.Failure(
                        batchResult.Error);

                foreach (var (handle, vector) in batchResult.Value)
                {
                    var key = HandleKey(handle);
                    if (indexMap.TryGetValue(key, out var idx))
                        results[idx] = (handle, vector);
                }
            }
        }

        // Collect ordered results, skipping any handles not returned by the store
        var ordered = results
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        return Result<IReadOnlyList<(VectorHandle, float[])>, string>.Success(ordered);
    }

    private static string HandleKey(VectorHandle h)
        => $"{h.ProviderId}\x1f{h.CollectionName}\x1f{h.VectorId}";
}

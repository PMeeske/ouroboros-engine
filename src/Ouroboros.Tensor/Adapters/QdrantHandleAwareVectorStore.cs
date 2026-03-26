// <copyright file="QdrantHandleAwareVectorStore.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Adapts a <see cref="QdrantClient"/> as an <see cref="IHandleAwareVectorStore"/>, enabling
/// the tensor pipeline to retrieve vectors from Qdrant using lightweight <see cref="VectorHandle"/>
/// references without materialising data until needed (R06, R07, R08).
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="VectorHandle.CollectionName"/> maps directly to a Qdrant collection.
/// The <see cref="VectorHandle.VectorId"/> is parsed as a GUID (UUID string) or a
/// <see langword="ulong"/> numeric ID. Both formats are supported by Qdrant.
/// </para>
/// <para>
/// Batch fetch groups handles by collection and issues one <c>GetAsync</c> call per collection,
/// minimising round-trips (R08).
/// </para>
/// </remarks>
public sealed class QdrantHandleAwareVectorStore : IHandleAwareVectorStore
{
    private readonly QdrantClient _client;

    /// <summary>
    /// Initializes a new <see cref="QdrantHandleAwareVectorStore"/> using the given Qdrant client.
    /// </summary>
    public QdrantHandleAwareVectorStore(QdrantClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc/>
    public async Task<Result<float[], string>> FetchAsync(
        VectorHandle handle,
        CancellationToken cancellationToken = default)
    {
        if (!TryParsePointId(handle.VectorId, out var pointId))
            return Result<float[], string>.Failure(
                $"VectorId '{handle.VectorId}' is not a valid Qdrant point ID " +
                "(expected a GUID string or a numeric ulong).");

        try
        {
            var points = await _client.GetAsync(
                handle.CollectionName,
                ids: new[] { pointId },
                withPayload: false,
                withVectors: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var point = points.FirstOrDefault();
            if (point is null)
                return Result<float[], string>.Failure(
                    $"Vector '{handle.VectorId}' not found in collection '{handle.CollectionName}'.");

            return ExtractVector(point, handle);
        }
        catch (Exception ex)
        {
            return Result<float[], string>.Failure($"Qdrant fetch failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<(VectorHandle Handle, float[] Vector)>, string>> FetchBatchAsync(
        IEnumerable<VectorHandle> handles,
        int batchSize = 64,
        CancellationToken cancellationToken = default)
    {
        var handleList = handles.ToList();
        var results = new List<(VectorHandle, float[])>(handleList.Count);

        // Group by collection: one GetAsync call per collection minimises round-trips (R08)
        var byCollection = handleList.GroupBy(h => h.CollectionName);

        foreach (var group in byCollection)
        {
            var collectionHandles = group.ToList();
            var pointIds = new List<PointId>(collectionHandles.Count);
            var idIndex = new Dictionary<string, VectorHandle>(collectionHandles.Count);

            foreach (var handle in collectionHandles)
            {
                if (!TryParsePointId(handle.VectorId, out var pid))
                    return Result<IReadOnlyList<(VectorHandle, float[])>, string>.Failure(
                        $"VectorId '{handle.VectorId}' is not a valid Qdrant point ID.");

                pointIds.Add(pid);
                idIndex[handle.VectorId] = handle;
            }

            try
            {
                var points = await _client.GetAsync(
                    group.Key,
                    ids: pointIds,
                    withPayload: false,
                    withVectors: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                foreach (var point in points)
                {
                    var vectorId = PointIdToString(point.Id);
                    if (!idIndex.TryGetValue(vectorId, out var handle))
                        continue;

                    var vectorResult = ExtractVector(point, handle);
                    if (vectorResult.IsSuccess)
                        results.Add((handle, vectorResult.Value));
                }
            }
            catch (Exception ex)
            {
                return Result<IReadOnlyList<(VectorHandle, float[])>, string>.Failure(
                    $"Qdrant batch fetch for collection '{group.Key}' failed: {ex.Message}");
            }
        }

        return Result<IReadOnlyList<(VectorHandle, float[])>, string>.Success(results);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<VectorHandle>, string>> SearchAsync(
        float[] queryVector,
        string collectionName,
        int topK,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryVector);
        ArgumentNullException.ThrowIfNull(collectionName);

        try
        {
            var scored = await _client.SearchAsync(
                collectionName,
                vector: new ReadOnlyMemory<float>(queryVector),
                limit: (ulong)topK,
                payloadSelector: false,
                vectorsSelector: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var handles = scored
                .Select(s => new VectorHandle(
                    ProviderId: "qdrant",
                    CollectionName: collectionName,
                    VectorId: PointIdToString(s.Id),
                    Dimension: queryVector.Length))
                .ToList();

            return Result<IReadOnlyList<VectorHandle>, string>.Success(handles);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<VectorHandle>, string>.Failure(
                $"Qdrant search in '{collectionName}' failed: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool TryParsePointId(string id, out PointId pointId)
    {
        if (Guid.TryParse(id, out var guid))
        {
            pointId = new PointId { Uuid = guid.ToString() };
            return true;
        }

        if (ulong.TryParse(id, out var num))
        {
            pointId = new PointId { Num = num };
            return true;
        }

        pointId = default!;
        return false;
    }

    private static string PointIdToString(PointId id)
        => id.IdCase == PointId.IdOneofCase.Uuid ? id.Uuid : id.Num.ToString();

    private static Result<float[], string> ExtractVector(RetrievedPoint point, VectorHandle handle)
    {
        // Default (un-named) vector lives in Vectors.Vector.Data
        if (point.Vectors?.Vector?.Data is { Count: > 0 } data)
            return Result<float[], string>.Success(data.ToArray());

        return Result<float[], string>.Failure(
            $"Point '{handle.VectorId}' in collection '{handle.CollectionName}' " +
            "has no default vector. If using named vectors, specify a vector name.");
    }
}

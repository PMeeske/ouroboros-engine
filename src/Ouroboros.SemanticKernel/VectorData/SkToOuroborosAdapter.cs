// <copyright file="SkToOuroborosAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Ouroboros.Domain.DocumentLoaders;
using Ouroboros.Domain.Vectors;
using Microsoft.Extensions.VectorData;
using Ouroboros.Domain.Vectors;
using SkVectorStore = Microsoft.Extensions.VectorData.VectorStore;

namespace Ouroboros.SemanticKernel.VectorData;

/// <summary>
/// Adapts an SK <see cref="SkVectorStore"/> to Ouroboros' <see cref="IAdvancedVectorStore"/>.
/// Operates on a single named collection using a <see cref="VectorStoreDocumentRecord"/>
/// as the SK record model.
/// </summary>
internal sealed class SkToOuroborosAdapter : IAdvancedVectorStore
{
    private readonly SkVectorStore _skStore;
    private readonly string _collectionName;
    private readonly int _vectorDimension;
    private readonly VectorStoreCollectionDefinition _definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkToOuroborosAdapter"/> class.
    /// </summary>
    internal SkToOuroborosAdapter(SkVectorStore skStore, string collectionName, int vectorDimension)
    {
        ArgumentNullException.ThrowIfNull(skStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        _skStore = skStore;
        _collectionName = collectionName;
        _vectorDimension = vectorDimension;
        _definition = VectorStoreDocumentRecord.BuildDefinition(vectorDimension);
    }

    /// <inheritdoc />
    public async Task AddAsync(IEnumerable<Vector> vectors, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectors);

        var collection = GetCollection();
        await collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);

        var records = vectors.Select(VectorStoreDocumentRecord.FromLangChainVector);
        await collection.UpsertAsync(records, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Document>> GetSimilarDocumentsAsync(
        float[] embedding,
        int amount = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        var collection = GetCollection();
        var results = new List<Document>();

        await foreach (var result in collection
            .SearchAsync(new ReadOnlyMemory<float>(embedding), amount, cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            results.Add(result.Record.ToDocument());
        }

        return results;
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();

        if (await collection.CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            await collection.EnsureCollectionDeletedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public IEnumerable<Vector> GetAll()
    {
        throw new NotSupportedException(
            "SK VectorStore does not support synchronous GetAll. Use SearchWithFilterAsync instead.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Document>> SearchWithFilterAsync(
        float[] embedding,
        IDictionary<string, object>? filter = null,
        int amount = 5,
        float? scoreThreshold = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        var collection = GetCollection();
        var options = new VectorSearchOptions<VectorStoreDocumentRecord>();

        // Note: SK VectorStore abstraction does not support metadata filtering.
        // The filter parameter is accepted for interface compatibility but not applied.

        // Apply score threshold via client-side filtering after retrieval,
        // since the base VectorSearchOptions does not expose a threshold parameter.
        var results = new List<Document>();

        await foreach (var result in collection
            .SearchAsync(new ReadOnlyMemory<float>(embedding), amount, options, cancellationToken)
            .ConfigureAwait(false))
        {
            if (scoreThreshold.HasValue && result.Score.HasValue && result.Score.Value < scoreThreshold.Value)
            {
                continue;
            }

            results.Add(result.Record.ToDocument());
        }

        return results;
    }

    /// <inheritdoc />
    public Task<ulong> CountAsync(IDictionary<string, object>? filter = null, CancellationToken cancellationToken = default)
    {
        // VectorStoreCollection does not expose a count operation in the base abstraction.
        throw new NotSupportedException(
            "Count is not supported by the SK VectorStore abstraction. Use the underlying provider directly.");
    }

    /// <inheritdoc />
    public Task<ScrollResult> ScrollAsync(
        int limit = 10,
        string? offset = null,
        IDictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Scroll is not supported by the SK VectorStore abstraction. Use the underlying provider directly.");
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReadOnlyCollection<Document>>> BatchSearchAsync(
        IReadOnlyList<float[]> embeddings,
        int amount = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(embeddings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        // Implement as sequential searches since SK has no batch search.
        return BatchSearchCoreAsync(embeddings, amount, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<Document>> RecommendAsync(
        IReadOnlyList<string> positiveIds,
        IReadOnlyList<string>? negativeIds = null,
        int amount = 5,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Recommend is not supported by the SK VectorStore abstraction. Use Qdrant directly.");
    }

    /// <inheritdoc />
    public async Task DeleteByIdAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var collection = GetCollection();
        await collection.DeleteAsync(ids, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DeleteByFilterAsync(IDictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Filter-based deletion is not supported by the SK VectorStore abstraction. Use the underlying provider directly.");
    }

    /// <inheritdoc />
    public async Task<VectorStoreInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        bool exists = await _skStore.CollectionExistsAsync(_collectionName, cancellationToken).ConfigureAwait(false);

        return new VectorStoreInfo(
            Name: _collectionName,
            // VectorCount is not available from the SK abstraction; 0 does not imply an empty
            // collection -- it signals "unknown" because VectorStoreInfo.VectorCount is ulong
            // and cannot represent a sentinel like -1.
            VectorCount: 0,
            VectorDimension: _vectorDimension,
            Status: exists ? "ready" : "not_found");
    }

    private VectorStoreCollection<string, VectorStoreDocumentRecord> GetCollection()
    {
        return _skStore.GetCollection<string, VectorStoreDocumentRecord>(_collectionName, _definition);
    }

    private async Task<IReadOnlyList<IReadOnlyCollection<Document>>> BatchSearchCoreAsync(
        IReadOnlyList<float[]> embeddings,
        int amount,
        CancellationToken cancellationToken)
    {
        var tasks = embeddings.Select(e => GetSimilarDocumentsAsync(e, amount, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }
}

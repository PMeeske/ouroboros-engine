// <copyright file="EmbodimentMemoryFusion.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Memory;

using Microsoft.Extensions.Logging;
using Ouroboros.Agent.MetaAI.Affect;
using Ouroboros.Core.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;

/// <summary>
/// Qdrant-backed implementation of <see cref="IEmbodimentMemoryFusion"/>.
/// Provides unified memory fusion across embodied perceptions with hybrid search
/// combining dense vector similarity, payload filtering, and temporal decay rescoring.
/// </summary>
/// <remarks>
/// Direct Qdrant.Client usage with temporal decay rescoring, paginated scroll-and-update,
/// and keyword payload fields. These operations exceed SK VectorStore abstraction capabilities.
/// Migrate simple upsert/search paths as typed record support matures.
/// </remarks>
[Obsolete("Use IAdvancedVectorStore via SK Qdrant connector for new vector code. Embodiment fusion ops retained as direct Qdrant calls.")]
public sealed partial class EmbodimentMemoryFusion : IEmbodimentMemoryFusion, IAsyncDisposable
{
    private const string DefaultCollectionName = "iaret_embodiment_memory";

    private readonly QdrantClient _qdrantClient;
    private readonly IEmbeddingModel _embeddingModel;
    private readonly ILogger<EmbodimentMemoryFusion>? _logger;
    private readonly string _collectionName;
    private readonly SemaphoreSlim _collectionInitLock = new(1, 1);
    private volatile bool _collectionInitialized;

    /// <summary>
    /// Initializes a new instance using the DI-provided client and collection registry.
    /// </summary>
    public EmbodimentMemoryFusion(
        QdrantClient qdrantClient,
        IQdrantCollectionRegistry registry,
        IEmbeddingModel embeddingModel,
        ILogger<EmbodimentMemoryFusion>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(qdrantClient);
        _qdrantClient = qdrantClient;
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(embeddingModel);
        _embeddingModel = embeddingModel;
        _collectionName = registry.GetCollectionName(QdrantCollectionRole.EmbodimentMemory);
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance with an explicit collection name.
    /// </summary>
    public EmbodimentMemoryFusion(
        QdrantClient qdrantClient,
        IEmbeddingModel embeddingModel,
        string collectionName = DefaultCollectionName,
        ILogger<EmbodimentMemoryFusion>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(qdrantClient);
        _qdrantClient = qdrantClient;
        ArgumentNullException.ThrowIfNull(embeddingModel);
        _embeddingModel = embeddingModel;
        ArgumentNullException.ThrowIfNull(collectionName);
        _collectionName = collectionName;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<Guid, string>> StoreMemoryAsync(
        EmbodiedMemory memory,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(memory);
            ArgumentException.ThrowIfNullOrWhiteSpace(memory.Content);
            ArgumentException.ThrowIfNullOrWhiteSpace(memory.Modality);

            await EnsureCollectionInitializedAsync(ct).ConfigureAwait(false);

            var id = Guid.NewGuid();
            var timestamp = memory.Timestamp ?? DateTime.UtcNow;

            var embedding = await _embeddingModel.CreateEmbeddingsAsync(memory.Content, ct).ConfigureAwait(false);

            // Extract keywords for sparse matching
            var keywords = ExtractKeywords(memory.Content);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = id.ToString() },
                Vectors = embedding,
                Payload =
                {
                    ["content"] = memory.Content,
                    ["modality"] = memory.Modality,
                    ["importance"] = memory.Importance,
                    ["timestamp"] = timestamp.Ticks,
                    ["keywords"] = JsonSerializer.Serialize(keywords),
                },
            };

            // Store metadata entries as individual payload fields
            if (memory.Metadata is { Count: > 0 })
            {
                point.Payload["metadata"] = JsonSerializer.Serialize(memory.Metadata);
            }

            await _qdrantClient.UpsertAsync(_collectionName, new[] { point }, cancellationToken: ct).ConfigureAwait(false);

            _logger?.LogInformation(
                "Stored embodied memory {MemoryId} modality={Modality} importance={Importance:F2}",
                id, memory.Modality, memory.Importance);

            return Result<Guid, string>.Success(id);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to store embodied memory");
            return Result<Guid, string>.Failure($"Failed to store embodied memory: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<int, string>> ApplyTemporalDecayAsync(
        double decayRate = 0.01,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureCollectionInitializedAsync(ct).ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var updated = 0;
            PointId? nextOffset = null;

            while (true)
            {
                var scrollResult = await _qdrantClient.ScrollAsync(
                    _collectionName,
                    limit: 100,
                    offset: nextOffset,
                    cancellationToken: ct).ConfigureAwait(false);

                foreach (var point in scrollResult.Result)
                {
                    var ticks = (long)point.Payload["timestamp"].IntegerValue;
                    var timestamp = new DateTime(ticks, DateTimeKind.Utc);
                    var importance = point.Payload["importance"].DoubleValue;

                    var ageDays = (now - timestamp).TotalDays;
                    var decayedImportance = importance * Math.Exp(-decayRate * ageDays);

                    // Only update if decay is meaningful (>1% change)
                    if (Math.Abs(decayedImportance - importance) > 0.01)
                    {
                        var updatePoint = new PointStruct
                        {
                            Id = point.Id,
                            Vectors = point.Vectors.Vector.Data.ToArray(),
                            Payload =
                            {
                                ["content"] = point.Payload["content"].StringValue,
                                ["modality"] = point.Payload["modality"].StringValue,
                                ["importance"] = Math.Max(0.0, decayedImportance),
                                ["timestamp"] = ticks,
                                ["keywords"] = point.Payload["keywords"].StringValue,
                            },
                        };

                        if (point.Payload.TryGetValue("metadata", out var metadataValue))
                        {
                            updatePoint.Payload["metadata"] = metadataValue.StringValue;
                        }

                        await _qdrantClient.UpsertAsync(
                            _collectionName, new[] { updatePoint }, cancellationToken: ct).ConfigureAwait(false);
                        updated++;
                    }
                }

                nextOffset = scrollResult.NextPageOffset;
                if (nextOffset is null || scrollResult.Result.Count == 0)
                {
                    break;
                }
            }

            _logger?.LogInformation("Applied temporal decay to {Count} memories (rate={Rate:F3})", updated, decayRate);
            return Result<int, string>.Success(updated);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to apply temporal decay");
            return Result<int, string>.Failure($"Failed to apply temporal decay: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _collectionInitLock.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task EnsureCollectionInitializedAsync(CancellationToken ct)
    {
        if (_collectionInitialized)
        {
            return;
        }

        await _collectionInitLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_collectionInitialized)
            {
                return;
            }

            var exists = await _qdrantClient.CollectionExistsAsync(_collectionName, ct).ConfigureAwait(false);

            if (!exists)
            {
                _logger?.LogInformation("Creating collection: {CollectionName}", _collectionName);

                uint vectorSize = 1536;
                try
                {
                    float[] probe = await _embeddingModel.CreateEmbeddingsAsync("probe", ct).ConfigureAwait(false);
                    vectorSize = (uint)probe.Length;
                    _logger?.LogInformation(
                        "Probed embedding dimension: {VectorSize} for collection {CollectionName}",
                        vectorSize, _collectionName);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger?.LogWarning(
                        ex,
                        "Failed to probe embedding dimension, falling back to default size {DefaultSize}",
                        vectorSize);
                }

                await _qdrantClient.CreateCollectionAsync(
                    _collectionName,
                    new VectorParams { Size = vectorSize, Distance = Distance.Cosine },
                    cancellationToken: ct).ConfigureAwait(false);
            }

            _collectionInitialized = true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize collection");
            throw;
        }
        finally
        {
            _collectionInitLock.Release();
        }
    }

    private static string[] ExtractKeywords(string content)
    {
        // Simple keyword extraction: split on whitespace/punctuation, deduplicate, lowercase
        return content
            .Split(new[] { ' ', ',', '.', ';', ':', '!', '?', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant().Trim())
            .Where(w => w.Length > 2)
            .Distinct()
            .ToArray();
    }
}

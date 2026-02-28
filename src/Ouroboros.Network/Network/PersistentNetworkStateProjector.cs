// <copyright file="PersistentNetworkStateProjector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Network;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Providers.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;

/// <summary>
/// A persistent version of NetworkStateProjector that saves snapshots and learnings to Qdrant.
/// Enables state recovery across sessions and continuous learning accumulation.
/// </summary>
public sealed class PersistentNetworkStateProjector : IAsyncDisposable
{
    private readonly string _snapshotCollectionName;
    private readonly string _learningsCollectionName;
    private const float DefaultScoreThreshold = 0.6f;
    private const int DefaultScrollLimit = 100;

    private readonly MerkleDag _dag;
    private readonly QdrantClient _qdrantClient;
    private readonly Func<string, Task<float[]>> _embeddingFunc;
    private readonly List<GlobalNetworkState> _snapshots;
    private readonly List<Learning> _recentLearnings;
    private readonly ILogger _logger;
    private long _currentEpoch;
    private bool _initialized;
    private int _detectedVectorDimension;

    /// <summary>
    /// Initializes a new instance using the DI-provided client and collection registry.
    /// </summary>
    public PersistentNetworkStateProjector(
        MerkleDag dag,
        QdrantClient client,
        IQdrantCollectionRegistry registry,
        Func<string, Task<float[]>> embeddingFunc,
        ILogger<PersistentNetworkStateProjector>? logger = null)
    {
        _dag = dag ?? throw new ArgumentNullException(nameof(dag));
        _qdrantClient = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentNullException.ThrowIfNull(registry);
        _embeddingFunc = embeddingFunc ?? throw new ArgumentNullException(nameof(embeddingFunc));
        _logger = logger ?? NullLogger<PersistentNetworkStateProjector>.Instance;
        _snapshotCollectionName = registry.GetCollectionName(QdrantCollectionRole.NetworkSnapshots);
        _learningsCollectionName = registry.GetCollectionName(QdrantCollectionRole.NetworkLearnings);
        _snapshots = new List<GlobalNetworkState>();
        _recentLearnings = new List<Learning>();
        _currentEpoch = 0;
        _initialized = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistentNetworkStateProjector"/> class.
    /// </summary>
    /// <param name="dag">The Merkle-DAG to project from.</param>
    /// <param name="qdrantEndpoint">The Qdrant endpoint (e.g., <see cref="DefaultEndpoints.QdrantGrpc"/>).</param>
    /// <param name="embeddingFunc">Function to generate embeddings for semantic storage.</param>
    [Obsolete("Use the constructor accepting QdrantClient + IQdrantCollectionRegistry from DI.")]
    public PersistentNetworkStateProjector(
        MerkleDag dag,
        string qdrantEndpoint,
        Func<string, Task<float[]>> embeddingFunc)
    {
        _dag = dag ?? throw new ArgumentNullException(nameof(dag));
        _embeddingFunc = embeddingFunc ?? throw new ArgumentNullException(nameof(embeddingFunc));
        _snapshotCollectionName = "network_state_snapshots";
        _learningsCollectionName = "network_learnings";
        var normalizedEndpoint = NormalizeEndpoint(qdrantEndpoint, DefaultEndpoints.QdrantGrpc);
        var endpointUri = new Uri(normalizedEndpoint, UriKind.Absolute);
        var host = endpointUri.Host;
        var port = endpointUri.Port > 0 ? endpointUri.Port : 6334;
        var useHttps = endpointUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        _qdrantClient = new QdrantClient(host, port, useHttps);
        _snapshots = new List<GlobalNetworkState>();
        _recentLearnings = new List<Learning>();
        _currentEpoch = 0;
        _initialized = false;
    }

    private static string NormalizeEndpoint(string? rawEndpoint, string fallbackEndpoint)
    {
        var endpoint = (rawEndpoint ?? string.Empty).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return fallbackEndpoint;
        }

        var schemeSeparatorCount = endpoint.Split("://", StringSplitOptions.None).Length - 1;
        if (schemeSeparatorCount > 1)
        {
            return fallbackEndpoint;
        }

        if (!endpoint.Contains("://", StringComparison.Ordinal))
        {
            endpoint = $"http://{endpoint}";
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return fallbackEndpoint;
        }

        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return fallbackEndpoint;
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || uri.Host.Contains("://", StringComparison.Ordinal))
        {
            return fallbackEndpoint;
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    /// <summary>
    /// Gets all loaded snapshots.
    /// </summary>
    public IReadOnlyList<GlobalNetworkState> Snapshots => _snapshots;

    /// <summary>
    /// Gets the current epoch number.
    /// </summary>
    public long CurrentEpoch => _currentEpoch;

    /// <summary>
    /// Gets recent learnings (from current session + loaded from Qdrant).
    /// </summary>
    public IReadOnlyList<Learning> RecentLearnings => _recentLearnings;

    /// <summary>
    /// Initializes the persistent projector by loading previous state from Qdrant.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        // Detect embedding dimension from the actual model
        var probe = await _embeddingFunc("dimension probe");
        _detectedVectorDimension = probe.Length;

        await EnsureCollectionsExistAsync(ct);
        await LoadPreviousStateAsync(ct);
        _initialized = true;
    }

    /// <summary>
    /// Projects and persists the current global network state.
    /// This should be called during "thinking" to capture learnings.
    /// </summary>
    /// <param name="metadata">Optional metadata to include.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created and persisted snapshot.</returns>
    public async Task<GlobalNetworkState> ProjectAndPersistAsync(
        ImmutableDictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(ct);
        }

        var nodeCountByType = _dag.Nodes.Values
            .GroupBy(n => n.TypeName)
            .ToImmutableDictionary(g => g.Key, g => g.Count());

        var transitionCountByOperation = _dag.Edges.Values
            .GroupBy(e => e.OperationName)
            .ToImmutableDictionary(g => g.Key, g => g.Count());

        var rootNodeIds = _dag.GetRootNodes().Select(n => n.Id).ToImmutableArray();
        var leafNodeIds = _dag.GetLeafNodes().Select(n => n.Id).ToImmutableArray();

        var transitionsWithConfidence = _dag.Edges.Values
            .Where(e => e.Confidence.HasValue)
            .ToList();

        var averageConfidence = transitionsWithConfidence.Any()
            ? transitionsWithConfidence.Average(e => e.Confidence!.Value)
            : (double?)null;

        var totalProcessingTimeMs = _dag.Edges.Values
            .Where(e => e.DurationMs.HasValue)
            .Sum(e => e.DurationMs!.Value);

        var totalProcessingTime = totalProcessingTimeMs > 0 ? (long?)totalProcessingTimeMs : null;

        var state = new GlobalNetworkState(
            _currentEpoch,
            DateTimeOffset.UtcNow,
            _dag.NodeCount,
            _dag.EdgeCount,
            nodeCountByType,
            transitionCountByOperation,
            rootNodeIds,
            leafNodeIds,
            averageConfidence,
            totalProcessingTime,
            metadata);

        _snapshots.Add(state);

        await PersistSnapshotAsync(state, ct);

        _currentEpoch++;
        return state;
    }

    /// <summary>
    /// Records a learning from the thinking process for persistent storage.
    /// </summary>
    /// <param name="category">Learning category (e.g., "skill", "pattern", "insight").</param>
    /// <param name="content">The content of what was learned.</param>
    /// <param name="context">The context in which it was learned.</param>
    /// <param name="confidence">Confidence level 0-1.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RecordLearningAsync(
        string category,
        string content,
        string context,
        double confidence = 1.0,
        CancellationToken ct = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(ct);
        }

        var learning = new Learning(
            Id: Guid.NewGuid().ToString("N"),
            Category: category,
            Content: content,
            Context: context,
            Confidence: confidence,
            Epoch: _currentEpoch,
            Timestamp: DateTimeOffset.UtcNow);

        _recentLearnings.Add(learning);

        await PersistLearningAsync(learning, ct);
    }

    /// <summary>
    /// Retrieves relevant learnings for a given context using semantic search.
    /// </summary>
    /// <param name="context">The context to search for.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of relevant learnings.</returns>
    public async Task<List<Learning>> GetRelevantLearningsAsync(
        string context,
        int limit = 5,
        CancellationToken ct = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(ct);
        }

        try
        {
            var embedding = await _embeddingFunc(context);

            var results = await _qdrantClient.SearchAsync(
                _learningsCollectionName,
                embedding,
                limit: (ulong)limit,
                scoreThreshold: DefaultScoreThreshold,
                cancellationToken: ct);

            var learnings = new List<Learning>();
            foreach (var result in results)
            {
                if (result.Payload.TryGetValue("learning_json", out var jsonValue))
                {
                    var learning = JsonSerializer.Deserialize<Learning>(jsonValue.StringValue);
                    if (learning != null)
                    {
                        learnings.Add(learning);
                    }
                }
            }

            return learnings;
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve learnings");
            return new List<Learning>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve learnings");
            return new List<Learning>();
        }
    }

    /// <summary>
    /// Gets all learnings from a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of learnings in that category.</returns>
    public async Task<List<Learning>> GetLearningsByCategoryAsync(
        string category,
        CancellationToken ct = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(ct);
        }

        try
        {
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "category",
                            Match = new Match { Keyword = category },
                        },
                    },
                },
            };

            var results = await _qdrantClient.ScrollAsync(
                _learningsCollectionName,
                filter: filter,
                limit: DefaultScrollLimit,
                cancellationToken: ct);

            var learnings = new List<Learning>();
            foreach (var point in results.Result)
            {
                if (point.Payload.TryGetValue("learning_json", out var jsonValue))
                {
                    var learning = JsonSerializer.Deserialize<Learning>(jsonValue.StringValue);
                    if (learning != null)
                    {
                        learnings.Add(learning);
                    }
                }
            }

            return learnings;
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve learnings by category");
            return new List<Learning>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve learnings by category");
            return new List<Learning>();
        }
    }

    private async Task EnsureCollectionsExistAsync(CancellationToken ct)
    {
        try
        {
            await EnsureCollectionWithDimensionAsync(_snapshotCollectionName, ct);
            await EnsureCollectionWithDimensionAsync(_learningsCollectionName, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to create Qdrant collections");
        }
    }

    private async Task EnsureCollectionWithDimensionAsync(string collectionName, CancellationToken ct)
    {
        var vectorParams = new VectorParams { Size = (ulong)_detectedVectorDimension, Distance = Distance.Cosine };

        if (await _qdrantClient.CollectionExistsAsync(collectionName, ct))
        {
            var info = await _qdrantClient.GetCollectionInfoAsync(collectionName, ct);
            var currentDim = info.Config?.Params?.VectorsConfig?.Params?.Size;
            if (currentDim.HasValue && currentDim.Value != (ulong)_detectedVectorDimension)
            {
                _logger.LogInformation("Dimension mismatch in {CollectionName} ({CurrentDim} vs {ExpectedDim}), recreating...", collectionName, currentDim, _detectedVectorDimension);
                await _qdrantClient.DeleteCollectionAsync(collectionName, cancellationToken: ct);
                await _qdrantClient.CreateCollectionAsync(collectionName, vectorParams, cancellationToken: ct);
            }
        }
        else
        {
            await _qdrantClient.CreateCollectionAsync(collectionName, vectorParams, cancellationToken: ct);
        }
    }

    private async Task LoadPreviousStateAsync(CancellationToken ct)
    {
        try
        {
            var scrollResult = await _qdrantClient.ScrollAsync(
                _snapshotCollectionName,
                limit: DefaultScrollLimit,
                cancellationToken: ct);

            GlobalNetworkState? latestSnapshot = null;
            long maxEpoch = -1;

            foreach (var point in scrollResult.Result)
            {
                if (point.Payload.TryGetValue("snapshot_json", out var jsonValue))
                {
                    var snapshot = JsonSerializer.Deserialize<GlobalNetworkState>(jsonValue.StringValue);
                    if (snapshot != null)
                    {
                        _snapshots.Add(snapshot);
                        if (snapshot.Epoch > maxEpoch)
                        {
                            maxEpoch = snapshot.Epoch;
                            latestSnapshot = snapshot;
                        }
                    }
                }
            }

            if (latestSnapshot != null)
            {
                _currentEpoch = latestSnapshot.Epoch + 1;
                _logger.LogInformation("Resumed from epoch {Epoch} ({SnapshotCount} snapshots loaded)", latestSnapshot.Epoch, _snapshots.Count);
            }

            var learningsResult = await _qdrantClient.ScrollAsync(
                _learningsCollectionName,
                limit: DefaultScrollLimit,
                cancellationToken: ct);

            foreach (var point in learningsResult.Result)
            {
                if (point.Payload.TryGetValue("learning_json", out var jsonValue))
                {
                    var learning = JsonSerializer.Deserialize<Learning>(jsonValue.StringValue);
                    if (learning != null)
                    {
                        _recentLearnings.Add(learning);
                    }
                }
            }

            if (_recentLearnings.Count > 0)
            {
                _logger.LogInformation("Loaded {LearningCount} previous learnings", _recentLearnings.Count);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to load previous state");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to load previous state");
        }
    }

    private async Task PersistSnapshotAsync(GlobalNetworkState state, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(state);
            var embedding = await _embeddingFunc($"network state epoch {state.Epoch} nodes {state.TotalNodes} transitions {state.TotalTransitions}");

            var point = new PointStruct
            {
                Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                Vectors = embedding,
                Payload =
                {
                    ["epoch"] = state.Epoch,
                    ["total_nodes"] = state.TotalNodes,
                    ["total_transitions"] = state.TotalTransitions,
                    ["timestamp"] = state.Timestamp.ToString("O"),
                    ["snapshot_json"] = json,
                },
            };

            await _qdrantClient.UpsertAsync(_snapshotCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to persist snapshot");
        }
    }

    private async Task PersistLearningAsync(Learning learning, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(learning);
            var embedding = await _embeddingFunc($"{learning.Category}: {learning.Content}");

            var point = new PointStruct
            {
                Id = new PointId { Uuid = learning.Id },
                Vectors = embedding,
                Payload =
                {
                    ["category"] = learning.Category,
                    ["content"] = learning.Content,
                    ["context"] = learning.Context,
                    ["confidence"] = learning.Confidence,
                    ["epoch"] = learning.Epoch,
                    ["timestamp"] = learning.Timestamp.ToString("O"),
                    ["learning_json"] = json,
                },
            };

            await _qdrantClient.UpsertAsync(_learningsCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to persist learning");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_initialized && _dag.NodeCount > 0)
        {
            try
            {
                await ProjectAndPersistAsync(
                    ImmutableDictionary<string, string>.Empty.Add("event", "shutdown"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Ignore errors during disposal
            }
        }

        _qdrantClient.Dispose();
    }
}

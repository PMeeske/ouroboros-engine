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
public sealed partial class PersistentNetworkStateProjector : IAsyncDisposable
{
    private readonly string _snapshotCollectionName;
    private readonly string _learningsCollectionName;
    private const float DefaultScoreThreshold = 0.6f;
    private const int DefaultScrollLimit = 100;

    private readonly MerkleDag _dag;
    private readonly QdrantClient _qdrantClient;
    private readonly bool _disposeClient;
    private readonly Func<string, CancellationToken, Task<float[]>> _embeddingFunc;
    private readonly List<GlobalNetworkState> _snapshots;
    private readonly List<Learning> _recentLearnings;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ILogger _logger;
    private long _currentEpoch;
    private volatile bool _initialized;
    private int _detectedVectorDimension;

    /// <summary>
    /// Initializes a new instance using the DI-provided client and collection registry.
    /// </summary>
    public PersistentNetworkStateProjector(
        MerkleDag dag,
        QdrantClient client,
        IQdrantCollectionRegistry registry,
        Func<string, CancellationToken, Task<float[]>> embeddingFunc,
        ILogger<PersistentNetworkStateProjector>? logger = null)
    {
        _dag = dag ?? throw new ArgumentNullException(nameof(dag));
        _qdrantClient = client ?? throw new ArgumentNullException(nameof(client));
        _disposeClient = false;
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
        Func<string, CancellationToken, Task<float[]>> embeddingFunc)
    {
        _dag = dag ?? throw new ArgumentNullException(nameof(dag));
        _embeddingFunc = embeddingFunc ?? throw new ArgumentNullException(nameof(embeddingFunc));
        _snapshotCollectionName = "network_state_snapshots";
        _learningsCollectionName = "network_learnings";
        var normalizedEndpoint = NormalizeEndpoint(qdrantEndpoint, Ouroboros.Core.Configuration.DefaultEndpoints.QdrantGrpc);
        var endpointUri = new Uri(normalizedEndpoint, UriKind.Absolute);
        var host = endpointUri.Host;
        var port = endpointUri.Port > 0 ? endpointUri.Port : 6334;
        var useHttps = endpointUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        _qdrantClient = new QdrantClient(host, port, useHttps);
        _disposeClient = true;
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
    public IReadOnlyList<GlobalNetworkState> Snapshots { get { lock (_stateLock) { return _snapshots.ToList(); } } }

    /// <summary>
    /// Gets the current epoch number.
    /// </summary>
    public long CurrentEpoch => _currentEpoch;

    /// <summary>
    /// Gets recent learnings (from current session + loaded from Qdrant).
    /// </summary>
    public IReadOnlyList<Learning> RecentLearnings { get { lock (_stateLock) { return _recentLearnings.ToList(); } } }

    /// <summary>
    /// Initializes the persistent projector by loading previous state from Qdrant.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            // Detect embedding dimension from the actual model
            var probe = await _embeddingFunc("dimension probe", ct);
            _detectedVectorDimension = probe.Length;

            await EnsureCollectionsExistAsync(ct);
            await LoadPreviousStateAsync(ct);
            _initialized = true;
        }
        finally { _initLock.Release(); }
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

        lock (_stateLock) { _snapshots.Add(state); }

        await PersistSnapshotAsync(state, ct);

        Interlocked.Increment(ref _currentEpoch);
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

        lock (_stateLock) { _recentLearnings.Add(learning); }

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
            var embedding = await _embeddingFunc(context, ct);

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

            var allPoints = new List<RetrievedPoint>();
            PointId? nextOffset = null;
            do
            {
                var response = await _qdrantClient.ScrollAsync(
                    _learningsCollectionName,
                    filter: filter,
                    limit: DefaultScrollLimit,
                    offset: nextOffset,
                    cancellationToken: ct);
                allPoints.AddRange(response.Result);
                nextOffset = response.Result.Count == DefaultScrollLimit ? response.NextPageOffset : null;
            }
            while (nextOffset != null);

            var learnings = new List<Learning>();
            foreach (var point in allPoints)
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

}

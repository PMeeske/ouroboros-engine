// <copyright file="QdrantDagStore.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Ouroboros.Network;

/// <summary>
/// Persists MerkleDag nodes and transitions to Qdrant for durable storage and semantic search.
/// Enables loading DAG history across sessions and searching nodes by semantic similarity.
/// </summary>
/// <remarks>
/// Direct Qdrant.Client usage with two cross-linked collections (nodes, edges) and
/// topological sort on load. The dual-collection graph structure has no SK VectorStore equivalent.
/// Migrate simple upsert/search paths to IVectorStoreRecordCollection as typed record support matures.
/// </remarks>
[Obsolete("Use IAdvancedVectorStore via SK Qdrant connector for new vector code. DAG store ops retained as direct Qdrant calls.")]
public sealed partial class QdrantDagStore : IAsyncDisposable
{
    private const int DefaultQdrantPort = 6334;

    private readonly QdrantDagConfig _config;
    private readonly QdrantClient _client;
    private readonly Func<string, Task<float[]>>? _embeddingFunc;
    private readonly bool _disposeClient;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance using the DI-provided client and collection registry.
    /// </summary>
    public QdrantDagStore(
        QdrantClient client,
        IQdrantCollectionRegistry registry,
        QdrantSettings settings,
        Func<string, Task<float[]>>? embeddingFunc = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(settings);
        _disposeClient = false;
        _embeddingFunc = embeddingFunc;
        _config = new QdrantDagConfig(
            Endpoint: settings.GrpcEndpoint,
            NodesCollection: registry.GetCollectionName(QdrantCollectionRole.DagNodes),
            EdgesCollection: registry.GetCollectionName(QdrantCollectionRole.DagEdges),
            VectorSize: settings.DefaultVectorSize,
            UseHttps: settings.UseHttps);
    }

    /// <summary>
    /// Gets whether this store supports semantic search (has embedding function).
    /// </summary>
    public bool SupportsSemanticSearch => _embeddingFunc != null;

    /// <summary>
    /// Initializes the Qdrant collections for DAG storage.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            // Create nodes collection
            if (!await _client.CollectionExistsAsync(_config.NodesCollection, ct).ConfigureAwait(false))
            {
                await _client.CreateCollectionAsync(
                    _config.NodesCollection,
                    new VectorParams { Size = (ulong)_config.VectorSize, Distance = Distance.Cosine },
                    cancellationToken: ct).ConfigureAwait(false);
            }

            // Create edges collection (smaller vectors for edge metadata)
            if (!await _client.CollectionExistsAsync(_config.EdgesCollection, ct).ConfigureAwait(false))
            {
                await _client.CreateCollectionAsync(
                    _config.EdgesCollection,
                    new VectorParams { Size = (ulong)_config.VectorSize, Distance = Distance.Cosine },
                    cancellationToken: ct).ConfigureAwait(false);
            }

            _initialized = true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            throw new InvalidOperationException($"Failed to initialize Qdrant collections: {ex.Message}", ex);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Saves a MonadNode to Qdrant.
    /// </summary>
    /// <param name="node">The node to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<MonadNode>> SaveNodeAsync(MonadNode node, CancellationToken ct = default)
    {
        if (node == null)
        {
            return Result<MonadNode>.Failure("Node cannot be null");
        }

        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        try
        {
            // Generate embedding if function available
            var embedding = await GenerateNodeEmbeddingAsync(node).ConfigureAwait(false);

            var payload = new Dictionary<string, Value>
            {
                ["id"] = node.Id.ToString(),
                ["type_name"] = node.TypeName,
                ["payload_json"] = node.PayloadJson,
                ["created_at"] = node.CreatedAt.ToString("O"),
                ["parent_ids"] = string.Join(",", node.ParentIds.Select(p => p.ToString())),
                ["hash"] = node.Hash,
            };

            var point = new PointStruct
            {
                Id = new PointId { Uuid = node.Id.ToString() },
                Vectors = embedding,
                Payload = { payload },
            };

            await _client.UpsertAsync(_config.NodesCollection, new[] { point }, cancellationToken: ct).ConfigureAwait(false);
            return Result<MonadNode>.Success(node);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            return Result<MonadNode>.Failure($"Failed to save node: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves a TransitionEdge to Qdrant.
    /// </summary>
    /// <param name="edge">The edge to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<TransitionEdge>> SaveEdgeAsync(TransitionEdge edge, CancellationToken ct = default)
    {
        if (edge == null)
        {
            return Result<TransitionEdge>.Failure("Edge cannot be null");
        }

        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        try
        {
            // Generate embedding for the edge
            var embedding = await GenerateEdgeEmbeddingAsync(edge).ConfigureAwait(false);

            var payload = new Dictionary<string, Value>
            {
                ["id"] = edge.Id.ToString(),
                ["input_ids"] = string.Join(",", edge.InputIds.Select(i => i.ToString())),
                ["output_id"] = edge.OutputId.ToString(),
                ["operation_name"] = edge.OperationName,
                ["operation_spec_json"] = edge.OperationSpecJson,
                ["created_at"] = edge.CreatedAt.ToString("O"),
                ["hash"] = edge.Hash,
            };

            if (edge.Confidence.HasValue)
            {
                payload["confidence"] = edge.Confidence.Value;
            }

            if (edge.DurationMs.HasValue)
            {
                payload["duration_ms"] = edge.DurationMs.Value;
            }

            var point = new PointStruct
            {
                Id = new PointId { Uuid = edge.Id.ToString() },
                Vectors = embedding,
                Payload = { payload },
            };

            await _client.UpsertAsync(_config.EdgesCollection, new[] { point }, cancellationToken: ct).ConfigureAwait(false);
            return Result<TransitionEdge>.Success(edge);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            return Result<TransitionEdge>.Failure($"Failed to save edge: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves an entire MerkleDag to Qdrant.
    /// </summary>
    /// <param name="dag">The DAG to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the count of saved items.</returns>
    public async Task<Result<DagSaveResult>> SaveDagAsync(MerkleDag dag, CancellationToken ct = default)
    {
        if (dag == null)
        {
            return Result<DagSaveResult>.Failure("DAG cannot be null");
        }

        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        var nodesSaved = 0;
        var edgesSaved = 0;
        var errors = new List<string>();

        // Save all nodes
        foreach (var node in dag.Nodes.Values)
        {
            var result = await SaveNodeAsync(node, ct).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                nodesSaved++;
            }
            else
            {
                errors.Add($"Node {node.Id}: {result.Error}");
            }
        }

        // Save all edges
        foreach (var edge in dag.Edges.Values)
        {
            var result = await SaveEdgeAsync(edge, ct).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                edgesSaved++;
            }
            else
            {
                errors.Add($"Edge {edge.Id}: {result.Error}");
            }
        }

        return Result<DagSaveResult>.Success(new DagSaveResult(nodesSaved, edgesSaved, errors));
    }

    /// <summary>
    /// Loads all nodes and edges from Qdrant into a MerkleDag.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the loaded DAG.</returns>
    public async Task<Result<MerkleDag>> LoadDagAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        try
        {
            var dag = new MerkleDag();

            // Load all nodes
            var nodesResult = await LoadAllNodesAsync(ct).ConfigureAwait(false);
            if (!nodesResult.IsSuccess)
            {
                return Result<MerkleDag>.Failure($"Failed to load nodes: {nodesResult.Error}");
            }

            // Sort nodes topologically (parents before children)
            var sortedNodes = TopologicalSort(nodesResult.Value);

            // Add nodes to DAG in order
            foreach (var node in sortedNodes)
            {
                dag.AddNode(node);
            }

            // Load and add all edges
            var edgesResult = await LoadAllEdgesAsync(ct).ConfigureAwait(false);
            if (!edgesResult.IsSuccess)
            {
                return Result<MerkleDag>.Failure($"Failed to load edges: {edgesResult.Error}");
            }

            foreach (var edge in edgesResult.Value)
            {
                dag.AddEdge(edge);
            }

            return Result<MerkleDag>.Success(dag);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            return Result<MerkleDag>.Failure($"Failed to load DAG: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_disposeClient)
            {
                _client.Dispose();
            }

            _initLock.Dispose();
            _disposed = true;
        }

        return ValueTask.CompletedTask;
    }
}
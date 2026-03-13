// <copyright file="PersistentMerkleDag.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Network.Persistence;

/// <summary>
/// Provides persistent storage for a Merkle-DAG using a Write-Ahead Log.
/// Wraps a MerkleDag instance and ensures all mutations are durably logged.
/// Supports crash recovery via WAL replay.
/// </summary>
public sealed class PersistentMerkleDag : IAsyncDisposable
{
    private readonly MerkleDag _dag;
    private readonly IGraphPersistence _persistence;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistentMerkleDag"/> class.
    /// </summary>
    /// <param name="dag">The underlying MerkleDag instance.</param>
    /// <param name="persistence">The persistence layer.</param>
    private PersistentMerkleDag(MerkleDag dag, IGraphPersistence persistence)
    {
        ArgumentNullException.ThrowIfNull(dag);
        _dag = dag;
        ArgumentNullException.ThrowIfNull(persistence);
        _persistence = persistence;
    }

    /// <summary>
    /// Gets all nodes in the DAG.
    /// </summary>
    public IReadOnlyDictionary<Guid, MonadNode> Nodes => _dag.Nodes;

    /// <summary>
    /// Gets all edges in the DAG.
    /// </summary>
    public IReadOnlyDictionary<Guid, TransitionEdge> Edges => _dag.Edges;

    /// <summary>
    /// Gets the count of nodes in the DAG.
    /// </summary>
    public int NodeCount => _dag.NodeCount;

    /// <summary>
    /// Gets the count of edges in the DAG.
    /// </summary>
    public int EdgeCount => _dag.EdgeCount;

    /// <summary>
    /// Restores a PersistentMerkleDag from a Write-Ahead Log.
    /// Replays all entries to rebuild the in-memory DAG state.
    /// Verifies integrity after replay.
    /// </summary>
    /// <param name="persistence">The persistence layer to restore from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the restored PersistentMerkleDag or an error message.</returns>
    public static async Task<Result<PersistentMerkleDag, string>> RestoreAsync(
        IGraphPersistence persistence,
        CancellationToken ct = default)
    {
        if (persistence == null)
        {
            return Result<PersistentMerkleDag, string>.Failure("Persistence layer cannot be null");
        }

        var dag = new MerkleDag();
        var replayErrors = new List<string>();

        try
        {
            await foreach (var entry in persistence.ReplayAsync(ct).ConfigureAwait(false))
            {
                switch (entry.EntryType)
                {
                    case nameof(WalEntryType.AddNode):
                        var node = JsonSerializer.Deserialize<MonadNode>(entry.DataJson);
                        if (node != null)
                        {
                            var nodeResult = dag.AddNode(node);
                            if (nodeResult.IsFailure)
                            {
                                replayErrors.Add($"Failed to replay node {node.Id}: {nodeResult.Error}");
                            }
                        }

                        break;

                    case nameof(WalEntryType.AddEdge):
                        var edge = JsonSerializer.Deserialize<TransitionEdge>(entry.DataJson);
                        if (edge != null)
                        {
                            var edgeResult = dag.AddEdge(edge);
                            if (edgeResult.IsFailure)
                            {
                                replayErrors.Add($"Failed to replay edge {edge.Id}: {edgeResult.Error}");
                            }
                        }

                        break;
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<PersistentMerkleDag, string>.Failure($"Replay failed: {ex.Message}");
        }

        if (replayErrors.Count > 0)
        {
            return Result<PersistentMerkleDag, string>.Failure(
                $"Replay completed with {replayErrors.Count} error(s): {string.Join("; ", replayErrors.Take(5))}");
        }

        // Verify integrity after replay
        var integrityResult = dag.VerifyIntegrity();
        if (integrityResult.IsFailure)
        {
            return Result<PersistentMerkleDag, string>.Failure($"Integrity check failed: {integrityResult.Error}");
        }

        return Result<PersistentMerkleDag, string>.Success(new PersistentMerkleDag(dag, persistence));
    }

    /// <summary>
    /// Creates a new empty PersistentMerkleDag with the specified persistence layer.
    /// </summary>
    /// <param name="persistence">The persistence layer.</param>
    /// <returns>A new PersistentMerkleDag instance.</returns>
    public static PersistentMerkleDag Create(IGraphPersistence persistence)
    {
        if (persistence == null)
        {
            throw new ArgumentNullException(nameof(persistence));
        }

        return new PersistentMerkleDag(new MerkleDag(), persistence);
    }

    /// <summary>
    /// Adds a node to the DAG and persists it to the WAL.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    /// <remarks>
    /// WARNING: If persistence fails after the node is added to the in-memory DAG,
    /// the system may be in an inconsistent state. The node will exist in memory but not in the WAL.
    /// In production systems, consider implementing a rollback mechanism or ensuring persistence
    /// completes before confirming the operation to callers.
    /// </remarks>
    public async Task<Result<MonadNode>> AddNodeAsync(MonadNode node, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        // Add to in-memory DAG first
        var result = _dag.AddNode(node);
        if (result.IsFailure)
        {
            return result;
        }

        // Persist to WAL
        // WARNING: If this fails, the node is already in memory, creating potential inconsistency.
        // Future enhancement: Implement rollback or two-phase commit.
        try
        {
            await _persistence.AppendNodeAsync(ToAbstractionsNode(node), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Note: The node is already in memory - in production, consider rollback or compensation
            return Result<MonadNode>.Failure($"Failed to persist node: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Adds an edge to the DAG and persists it to the WAL.
    /// </summary>
    /// <param name="edge">The edge to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    /// <remarks>
    /// WARNING: If persistence fails after the edge is added to the in-memory DAG,
    /// the system may be in an inconsistent state. The edge will exist in memory but not in the WAL.
    /// In production systems, consider implementing a rollback mechanism or ensuring persistence
    /// completes before confirming the operation to callers.
    /// </remarks>
    public async Task<Result<TransitionEdge>> AddEdgeAsync(TransitionEdge edge, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        // Add to in-memory DAG first
        var result = _dag.AddEdge(edge);
        if (result.IsFailure)
        {
            return result;
        }

        // Persist to WAL
        // WARNING: If this fails, the edge is already in memory, creating potential inconsistency.
        // Future enhancement: Implement rollback or two-phase commit.
        try
        {
            await _persistence.AppendEdgeAsync(ToAbstractionsEdge(edge), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Note: The edge is already in memory - in production, consider rollback or compensation
            return Result<TransitionEdge>.Failure($"Failed to persist edge: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Flushes all pending writes to durable storage.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous flush operation.</returns>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await _persistence.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a node by its ID.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>An Option containing the node if found.</returns>
    public Option<MonadNode> GetNode(Guid nodeId) => _dag.GetNode(nodeId);

    /// <summary>
    /// Gets an edge by its ID.
    /// </summary>
    /// <param name="edgeId">The edge ID.</param>
    /// <returns>An Option containing the edge if found.</returns>
    public Option<TransitionEdge> GetEdge(Guid edgeId) => _dag.GetEdge(edgeId);

    /// <summary>
    /// Gets all incoming edges for a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>A collection of incoming edges.</returns>
    public IEnumerable<TransitionEdge> GetIncomingEdges(Guid nodeId) => _dag.GetIncomingEdges(nodeId);

    /// <summary>
    /// Gets all outgoing edges for a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>A collection of outgoing edges.</returns>
    public IEnumerable<TransitionEdge> GetOutgoingEdges(Guid nodeId) => _dag.GetOutgoingEdges(nodeId);

    /// <summary>
    /// Gets all root nodes (nodes with no parents).
    /// </summary>
    /// <returns>A collection of root nodes.</returns>
    public IEnumerable<MonadNode> GetRootNodes() => _dag.GetRootNodes();

    /// <summary>
    /// Gets all leaf nodes (nodes with no outgoing edges).
    /// </summary>
    /// <returns>A collection of leaf nodes.</returns>
    public IEnumerable<MonadNode> GetLeafNodes() => _dag.GetLeafNodes();

    /// <summary>
    /// Performs a topological sort of the DAG.
    /// </summary>
    /// <returns>A Result containing the sorted nodes or an error if the graph has cycles.</returns>
    public Result<ImmutableArray<MonadNode>> TopologicalSort() => _dag.TopologicalSort();

    /// <summary>
    /// Gets all nodes of a specific type.
    /// </summary>
    /// <param name="typeName">The type name to filter by.</param>
    /// <returns>A collection of nodes with the specified type.</returns>
    public IEnumerable<MonadNode> GetNodesByType(string typeName) => _dag.GetNodesByType(typeName);

    /// <summary>
    /// Gets all transitions with a specific operation name.
    /// </summary>
    /// <param name="operationName">The operation name to filter by.</param>
    /// <returns>A collection of edges with the specified operation.</returns>
    public IEnumerable<TransitionEdge> GetTransitionsByOperation(string operationName) =>
        _dag.GetTransitionsByOperation(operationName);

    /// <summary>
    /// Verifies the integrity of the entire DAG.
    /// </summary>
    /// <returns>A Result indicating whether the DAG is valid.</returns>
    public Result<bool> VerifyIntegrity() => _dag.VerifyIntegrity();

    /// <summary>
    /// Disposes the persistent DAG and closes the persistence layer.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _persistence.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }

    private static Ouroboros.Abstractions.Network.MonadNode ToAbstractionsNode(MonadNode node) =>
        new(node.Id, node.TypeName, node.PayloadJson, node.CreatedAt, node.ParentIds, node.Hash);

    private static Ouroboros.Abstractions.Network.TransitionEdge ToAbstractionsEdge(TransitionEdge edge) =>
        new(
            edge.Id,
            edge.InputIds.Length > 0 ? edge.InputIds[0] : Guid.Empty,
            edge.OutputId,
            edge.OperationName,
            edge.CreatedAt,
            new Dictionary<string, object>());

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PersistentMerkleDag));
        }
    }
}

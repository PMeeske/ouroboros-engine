// <copyright file="NetworkStateProjector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Network;

/// <summary>
/// Service for projecting global network state from the Merkle-DAG.
/// Performs aggregation and fold operations over all nodes and transitions.
/// </summary>
public sealed class NetworkStateProjector
{
    private readonly MerkleDag _dag;
    private long _currentEpoch;
    private readonly List<GlobalNetworkState> _snapshots;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkStateProjector"/> class.
    /// </summary>
    /// <param name="dag">The Merkle-DAG to project from.</param>
    public NetworkStateProjector(MerkleDag dag)
    {
        _dag = dag ?? throw new ArgumentNullException(nameof(dag));
        _currentEpoch = 0;
        _snapshots = new List<GlobalNetworkState>();
    }

    /// <summary>
    /// Gets all historical snapshots.
    /// </summary>
    public IReadOnlyList<GlobalNetworkState> Snapshots => _snapshots;

    /// <summary>
    /// Gets the current epoch number.
    /// </summary>
    public long CurrentEpoch => _currentEpoch;

    /// <summary>
    /// Projects the current global network state from the DAG.
    /// </summary>
    /// <param name="metadata">Optional metadata to include in the snapshot.</param>
    /// <returns>The computed global network state.</returns>
    public GlobalNetworkState ProjectCurrentState(ImmutableDictionary<string, string>? metadata = null)
    {
        // Count nodes by type
        var nodeCountByType = _dag.Nodes.Values
            .GroupBy(n => n.TypeName)
            .ToImmutableDictionary(g => g.Key, g => g.Count());

        // Count transitions by operation
        var transitionCountByOperation = _dag.Edges.Values
            .GroupBy(e => e.OperationName)
            .ToImmutableDictionary(g => g.Key, g => g.Count());

        // Get root and leaf nodes
        var rootNodeIds = _dag.GetRootNodes().Select(n => n.Id).ToImmutableArray();
        var leafNodeIds = _dag.GetLeafNodes().Select(n => n.Id).ToImmutableArray();

        // Calculate average confidence
        var transitionsWithConfidence = _dag.Edges.Values
            .Where(e => e.Confidence.HasValue)
            .ToList();

        var averageConfidence = transitionsWithConfidence.Any()
            ? transitionsWithConfidence.Average(e => e.Confidence!.Value)
            : (double?)null;

        // Calculate total processing time
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

        return state;
    }

    /// <summary>
    /// Creates and stores a snapshot of the current global state.
    /// </summary>
    /// <param name="metadata">Optional metadata to include in the snapshot.</param>
    /// <returns>The created snapshot.</returns>
    public GlobalNetworkState CreateSnapshot(ImmutableDictionary<string, string>? metadata = null)
    {
        var state = ProjectCurrentState(metadata);
        _snapshots.Add(state);
        _currentEpoch++;
        return state;
    }

    /// <summary>
    /// Gets a snapshot by epoch number.
    /// </summary>
    /// <param name="epoch">The epoch number.</param>
    /// <returns>An Option containing the snapshot if found.</returns>
    public Option<GlobalNetworkState> GetSnapshot(long epoch)
    {
        var snapshot = _snapshots.FirstOrDefault(s => s.Epoch == epoch);
        return snapshot is not null ? Option<GlobalNetworkState>.Some(snapshot) : Option<GlobalNetworkState>.None();
    }

    /// <summary>
    /// Gets the most recent snapshot.
    /// </summary>
    /// <returns>An Option containing the most recent snapshot if any exist.</returns>
    public Option<GlobalNetworkState> GetLatestSnapshot()
    {
        var snapshot = _snapshots.LastOrDefault();
        return snapshot is not null ? Option<GlobalNetworkState>.Some(snapshot) : Option<GlobalNetworkState>.None();
    }

    /// <summary>
    /// Computes the difference between two snapshots.
    /// </summary>
    /// <param name="fromEpoch">The starting epoch.</param>
    /// <param name="toEpoch">The ending epoch.</param>
    /// <returns>A Result containing the state delta or an error.</returns>
    public Result<GlobalNetworkStateDelta> ComputeDelta(long fromEpoch, long toEpoch)
    {
        var fromSnapshot = GetSnapshot(fromEpoch);
        var toSnapshot = GetSnapshot(toEpoch);

        if (!fromSnapshot.HasValue)
        {
            return Result<GlobalNetworkStateDelta>.Failure($"Snapshot for epoch {fromEpoch} not found");
        }

        if (!toSnapshot.HasValue)
        {
            return Result<GlobalNetworkStateDelta>.Failure($"Snapshot for epoch {toEpoch} not found");
        }

        var from = fromSnapshot.Value!;
        var to = toSnapshot.Value!;

        var delta = new GlobalNetworkStateDelta(
            from.Epoch,
            to.Epoch,
            to.TotalNodes - from.TotalNodes,
            to.TotalTransitions - from.TotalTransitions,
            to.Timestamp);

        return Result<GlobalNetworkStateDelta>.Success(delta);
    }
}
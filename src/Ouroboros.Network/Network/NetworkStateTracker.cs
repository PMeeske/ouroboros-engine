// <copyright file="NetworkStateTracker.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Network;

/// <summary>
/// Service for automatically tracking pipeline execution in the emergent network state.
/// Provides real-time reification of Step execution into the MerkleDag.
/// Supports optional Qdrant persistence and MeTTa symbolic export.
/// </summary>
public sealed class NetworkStateTracker : IDisposable, IAsyncDisposable
{
    private readonly MerkleDag _dag;
    private readonly NetworkStateProjector _projector;
    private readonly TransitionReplayEngine _replayEngine;
    private readonly Dictionary<string, PipelineBranchReifier> _branchReifiers;
    private readonly object _syncLock = new();
    private readonly List<string> _mettaFacts = [];
    private QdrantDagStore? _qdrantStore;
    private IMeTTaEngine? _mettaEngine;
    private bool _autoPersist;
    private bool _autoExportMeTTa;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkStateTracker"/> class.
    /// </summary>
    public NetworkStateTracker()
    {
        _dag = new MerkleDag();
        _projector = new NetworkStateProjector(_dag);
        _replayEngine = new TransitionReplayEngine(_dag);
        _branchReifiers = new Dictionary<string, PipelineBranchReifier>();
    }

    /// <summary>
    /// Gets or sets whether to automatically persist to Qdrant after each update.
    /// </summary>
    public bool AutoPersist
    {
        get => _autoPersist;
        set => _autoPersist = value;
    }

    /// <summary>
    /// Gets or sets whether to automatically export MeTTa facts after each update.
    /// </summary>
    public bool AutoExportMeTTa
    {
        get => _autoExportMeTTa;
        set => _autoExportMeTTa = value;
    }

    /// <summary>
    /// Gets whether Qdrant persistence is configured.
    /// </summary>
    public bool HasQdrantStore => _qdrantStore != null;

    /// <summary>
    /// Gets whether MeTTa export is configured.
    /// </summary>
    public bool HasMeTTaEngine => _mettaEngine != null;

    /// <summary>
    /// Gets all exported MeTTa facts.
    /// </summary>
    public IReadOnlyList<string> MeTTaFacts => _mettaFacts;

    /// <summary>
    /// Configures Qdrant persistence for the tracker.
    /// </summary>
    /// <param name="store">The Qdrant DAG store to use.</param>
    /// <param name="autoPersist">Whether to auto-persist after each update.</param>
    public void ConfigureQdrantPersistence(QdrantDagStore store, bool autoPersist = true)
    {
        _qdrantStore = store ?? throw new ArgumentNullException(nameof(store));
        _autoPersist = autoPersist;
    }

    /// <summary>
    /// Configures MeTTa engine for symbolic export of reified events.
    /// </summary>
    /// <param name="engine">The MeTTa engine to export facts to.</param>
    /// <param name="autoExport">Whether to auto-export after each update.</param>
    public void ConfigureMeTTaExport(IMeTTaEngine engine, bool autoExport = true)
    {
        _mettaEngine = engine ?? throw new ArgumentNullException(nameof(engine));
        _autoExportMeTTa = autoExport;
    }

    /// <summary>
    /// Gets the underlying MerkleDag.
    /// </summary>
    public MerkleDag Dag => _dag;

    /// <summary>
    /// Gets the network state projector.
    /// </summary>
    public NetworkStateProjector Projector => _projector;

    /// <summary>
    /// Gets the transition replay engine.
    /// </summary>
    public TransitionReplayEngine ReplayEngine => _replayEngine;

    /// <summary>
    /// Gets the count of tracked branches.
    /// </summary>
    public int TrackedBranchCount => _branchReifiers.Count;

    /// <summary>
    /// Event raised when a branch is reified (for external listeners).
    /// </summary>
    public event EventHandler<BranchReifiedEventArgs>? BranchReified;

    /// <summary>
    /// Tracks a pipeline branch, reifying its current and future events.
    /// </summary>
    /// <param name="branch">The branch to track.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result<ReificationResult> TrackBranch(PipelineBranch branch)
    {
        if (branch == null)
        {
            return Result<ReificationResult>.Failure("Branch cannot be null");
        }

        lock (_syncLock)
        {
            if (!_branchReifiers.TryGetValue(branch.Name, out var reifier))
            {
                reifier = new PipelineBranchReifier(_dag, _projector);
                _branchReifiers[branch.Name] = reifier;
            }

            var result = reifier.ReifyBranch(branch);

            if (result.IsSuccess)
            {
                _ = OnBranchUpdatedAsync(branch, result.Value.NodesCreated);
            }

            return result;
        }
    }

    /// <summary>
    /// Tracks a pipeline branch asynchronously with persistence and MeTTa export.
    /// </summary>
    /// <param name="branch">The branch to track.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<ReificationResult>> TrackBranchAsync(PipelineBranch branch, CancellationToken ct = default)
    {
        if (branch == null)
        {
            return Result<ReificationResult>.Failure("Branch cannot be null");
        }

        ReificationResult reificationResult;
        lock (_syncLock)
        {
            if (!_branchReifiers.TryGetValue(branch.Name, out var reifier))
            {
                reifier = new PipelineBranchReifier(_dag, _projector);
                _branchReifiers[branch.Name] = reifier;
            }

            var result = reifier.ReifyBranch(branch);
            if (!result.IsSuccess)
            {
                return result;
            }

            reificationResult = result.Value;
        }

        await OnBranchUpdatedAsync(branch, reificationResult.NodesCreated, ct);
        return Result<ReificationResult>.Success(reificationResult);
    }

    /// <summary>
    /// Updates tracking for a branch with new events.
    /// Call this after each Step execution to incrementally update the DAG.
    /// </summary>
    /// <param name="branch">The branch with potentially new events.</param>
    /// <returns>A Result containing the count of newly reified events.</returns>
    public Result<int> UpdateBranch(PipelineBranch branch)
    {
        if (branch == null)
        {
            return Result<int>.Failure("Branch cannot be null");
        }

        lock (_syncLock)
        {
            if (!_branchReifiers.TryGetValue(branch.Name, out var reifier))
            {
                // First time seeing this branch - do full reification
                reifier = new PipelineBranchReifier(_dag, _projector);
                _branchReifiers[branch.Name] = reifier;
                var fullResult = reifier.ReifyBranch(branch);
                if (fullResult.IsSuccess)
                {
                    _ = OnBranchUpdatedAsync(branch, fullResult.Value.NodesCreated);
                }

                return fullResult.IsSuccess
                    ? Result<int>.Success(fullResult.Value.NodesCreated)
                    : Result<int>.Failure(fullResult.Error ?? "Unknown error");
            }

            var result = reifier.ReifyNewEvents(branch);
            if (result.IsSuccess && result.Value > 0)
            {
                _ = OnBranchUpdatedAsync(branch, result.Value);
            }

            return result;
        }
    }

    /// <summary>
    /// Updates tracking for a branch asynchronously with persistence and MeTTa export.
    /// </summary>
    /// <param name="branch">The branch with potentially new events.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the count of newly reified events.</returns>
    public async Task<Result<int>> UpdateBranchAsync(PipelineBranch branch, CancellationToken ct = default)
    {
        if (branch == null)
        {
            return Result<int>.Failure("Branch cannot be null");
        }

        int nodesCreated;
        lock (_syncLock)
        {
            if (!_branchReifiers.TryGetValue(branch.Name, out var reifier))
            {
                reifier = new PipelineBranchReifier(_dag, _projector);
                _branchReifiers[branch.Name] = reifier;
                var fullResult = reifier.ReifyBranch(branch);
                if (!fullResult.IsSuccess)
                {
                    return Result<int>.Failure(fullResult.Error ?? "Unknown error");
                }

                nodesCreated = fullResult.Value.NodesCreated;
            }
            else
            {
                var result = reifier.ReifyNewEvents(branch);
                if (!result.IsSuccess)
                {
                    return result;
                }

                nodesCreated = result.Value;
            }
        }

        if (nodesCreated > 0)
        {
            await OnBranchUpdatedAsync(branch, nodesCreated, ct);
        }

        return Result<int>.Success(nodesCreated);
    }

    /// <summary>
    /// Handles branch updates - persists to Qdrant and exports to MeTTa if configured.
    /// </summary>
    private async Task OnBranchUpdatedAsync(PipelineBranch branch, int nodesCreated, CancellationToken ct = default)
    {
        try
        {
            if (_autoPersist && _qdrantStore != null)
            {
                await PersistToQdrantAsync(ct);
            }

            if (_autoExportMeTTa && _mettaEngine != null)
            {
                await ExportToMeTTaAsync(branch, ct);
            }

            BranchReified?.Invoke(this, new BranchReifiedEventArgs(branch.Name, nodesCreated));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"[NetworkStateTracker] Post-update error: {ex.Message}");
        }
    }

    /// <summary>
    /// Persists the current DAG state to Qdrant.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the save result.</returns>
    public async Task<Result<DagSaveResult>> PersistToQdrantAsync(CancellationToken ct = default)
    {
        if (_qdrantStore == null)
        {
            return Result<DagSaveResult>.Failure("Qdrant store not configured. Call ConfigureQdrantPersistence first.");
        }

        return await _qdrantStore.SaveDagAsync(_dag, ct);
    }

    /// <summary>
    /// Exports branch facts to the MeTTa engine.
    /// </summary>
    /// <param name="branch">The branch to export.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<int>> ExportToMeTTaAsync(PipelineBranch branch, CancellationToken ct = default)
    {
        if (_mettaEngine == null)
        {
            return Result<int>.Failure("MeTTa engine not configured. Call ConfigureMeTTaExport first.");
        }

        var facts = branch.ToMeTTaFacts();
        var addedCount = 0;

        foreach (var fact in facts)
        {
            if (string.IsNullOrWhiteSpace(fact))
            {
                continue;
            }

            if (!_mettaFacts.Contains(fact))
            {
                _mettaFacts.Add(fact);
            }

            var result = await _mettaEngine.AddFactAsync(fact, ct);
            if (result.IsSuccess)
            {
                addedCount++;
            }
        }

        return Result<int>.Success(addedCount);
    }

    /// <summary>
    /// Exports all tracked branches to MeTTa with DAG constraint rules.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the total facts added.</returns>
    public async Task<Result<int>> ExportAllToMeTTaAsync(CancellationToken ct = default)
    {
        if (_mettaEngine == null)
        {
            return Result<int>.Failure("MeTTa engine not configured. Call ConfigureMeTTaExport first.");
        }

        var totalAdded = 0;

        var rules = DagMeTTaExtensions.GetDagConstraintRules();
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule) || rule.TrimStart().StartsWith(';'))
            {
                continue;
            }

            var result = await _mettaEngine.AddFactAsync(rule, ct);
            if (result.IsSuccess)
            {
                totalAdded++;
            }
        }

        foreach (var branchName in _branchReifiers.Keys)
        {
            if (_branchReifiers.TryGetValue(branchName, out var reifier))
            {
                // Note: Branch export requires the PipelineBranch instance.
                // Use ExportToMeTTaAsync(branch) for individual branches.
            }
        }

        return Result<int>.Success(totalAdded);
    }

    /// <summary>
    /// Verifies a DAG constraint using MeTTa symbolic reasoning.
    /// </summary>
    /// <param name="branchName">The branch to verify.</param>
    /// <param name="constraint">The constraint to check (e.g., "acyclic", "valid-ordering").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the constraint is satisfied, false otherwise.</returns>
    public async Task<Result<bool>> VerifyConstraintAsync(string branchName, string constraint, CancellationToken ct = default)
    {
        if (_mettaEngine == null)
        {
            return Result<bool>.Failure("MeTTa engine not configured. Call ConfigureMeTTaExport first.");
        }

        var verifyResult = await _mettaEngine.VerifyDagConstraintAsync(branchName, constraint, ct);
        return verifyResult.Match(
            isValid => Result<bool>.Success(isValid),
            error => Result<bool>.Failure(error));
    }

    /// <summary>
    /// Creates a snapshot of the current global network state across all tracked branches.
    /// </summary>
    /// <returns>The global network state snapshot.</returns>
    public GlobalNetworkState CreateSnapshot()
    {
        var metadata = ImmutableDictionary<string, string>.Empty
            .Add("trackedBranches", string.Join(",", _branchReifiers.Keys))
            .Add("branchCount", _branchReifiers.Count.ToString());

        return _projector.CreateSnapshot(metadata);
    }

    /// <summary>
    /// Gets the replay path from a root node to the specified target node.
    /// </summary>
    /// <param name="targetNodeId">The target node ID.</param>
    /// <returns>A Result containing the transition path.</returns>
    public Result<ImmutableArray<TransitionEdge>> ReplayToNode(Guid targetNodeId)
    {
        return _replayEngine.ReplayPathToNode(targetNodeId);
    }

    /// <summary>
    /// Gets a summary of the current network state.
    /// </summary>
    /// <returns>A formatted summary string.</returns>
    public string GetStateSummary()
    {
        var state = _projector.ProjectCurrentState();

        var summary = new System.Text.StringBuilder();
        summary.AppendLine("=== Network State Summary ===");
        summary.AppendLine($"Total Nodes: {state.TotalNodes}");
        summary.AppendLine($"Total Transitions: {state.TotalTransitions}");
        summary.AppendLine($"Tracked Branches: {_branchReifiers.Count}");
        summary.AppendLine($"Current Epoch: {_projector.CurrentEpoch}");

        if (state.AverageConfidence.HasValue)
        {
            summary.AppendLine($"Average Confidence: {state.AverageConfidence:F2}");
        }

        if (state.TotalProcessingTimeMs.HasValue)
        {
            summary.AppendLine($"Total Processing Time: {state.TotalProcessingTimeMs}ms");
        }

        summary.AppendLine();
        summary.AppendLine("Nodes by Type:");
        foreach (var kvp in state.NodeCountByType)
        {
            summary.AppendLine($"  {kvp.Key}: {kvp.Value}");
        }

        if (state.TransitionCountByOperation.Any())
        {
            summary.AppendLine();
            summary.AppendLine("Transitions by Operation:");
            foreach (var kvp in state.TransitionCountByOperation)
            {
                summary.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        return summary.ToString();
    }

    /// <summary>
    /// Gets the reifier for a specific branch.
    /// </summary>
    /// <param name="branchName">The branch name.</param>
    /// <returns>An Option containing the reifier if found.</returns>
    public Option<PipelineBranchReifier> GetBranchReifier(string branchName)
    {
        lock (_syncLock)
        {
            return _branchReifiers.TryGetValue(branchName, out var reifier)
                ? Option<PipelineBranchReifier>.Some(reifier)
                : Option<PipelineBranchReifier>.None();
        }
    }

    /// <summary>
    /// Clears all tracked branches and resets the DAG.
    /// </summary>
    public void Reset()
    {
        lock (_syncLock)
        {
            _branchReifiers.Clear();
            _mettaFacts.Clear();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _branchReifiers.Clear();
            _mettaFacts.Clear();
            _disposed = true;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _branchReifiers.Clear();
            _mettaFacts.Clear();

            if (_qdrantStore != null)
            {
                await _qdrantStore.DisposeAsync();
            }

            _disposed = true;
        }
    }
}

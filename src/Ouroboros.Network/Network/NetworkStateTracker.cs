// <copyright file="NetworkStateTracker.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Network;

/// <summary>
/// Service for automatically tracking pipeline execution in the emergent network state.
/// Provides real-time reification of Step execution into the MerkleDag.
/// Supports optional Qdrant persistence and MeTTa symbolic export.
/// </summary>
public sealed partial class NetworkStateTracker : IDisposable, IAsyncDisposable
{
    private readonly MerkleDag _dag;
    private readonly NetworkStateProjector _projector;
    private readonly TransitionReplayEngine _replayEngine;
    private readonly Dictionary<string, PipelineBranchReifier> _branchReifiers;
    private readonly object _syncLock = new();
    private readonly List<string> _mettaFacts = [];
    private readonly ILogger _logger;
    private QdrantDagStore? _qdrantStore;
    private IMeTTaEngine? _mettaEngine;
    private bool _autoPersist;
    private bool _autoExportMeTTa;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkStateTracker"/> class.
    /// </summary>
    public NetworkStateTracker(ILogger<NetworkStateTracker>? logger = null)
    {
        _logger = logger ?? NullLogger<NetworkStateTracker>.Instance;
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
        ArgumentNullException.ThrowIfNull(store);
        _qdrantStore = store;
        _autoPersist = autoPersist;
    }

    /// <summary>
    /// Configures MeTTa engine for symbolic export of reified events.
    /// </summary>
    /// <param name="engine">The MeTTa engine to export facts to.</param>
    /// <param name="autoExport">Whether to auto-export after each update.</param>
    public void ConfigureMeTTaExport(IMeTTaEngine engine, bool autoExport = true)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _mettaEngine = engine;
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

        await OnBranchUpdatedAsync(branch, reificationResult.NodesCreated, ct).ConfigureAwait(false);
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
            await OnBranchUpdatedAsync(branch, nodesCreated, ct).ConfigureAwait(false);
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
                await PersistToQdrantAsync(ct).ConfigureAwait(false);
            }

            if (_autoExportMeTTa && _mettaEngine != null)
            {
                await ExportToMeTTaAsync(branch, ct).ConfigureAwait(false);
            }

            BranchReified?.Invoke(this, new BranchReifiedEventArgs(branch.Name, nodesCreated));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Post-update error");
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

        return await _qdrantStore.SaveDagAsync(_dag, ct).ConfigureAwait(false);
    }

}

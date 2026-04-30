// <copyright file="NetworkStateTracker.Export.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Network;

/// <summary>
/// Partial class containing MeTTa export, Qdrant persistence, snapshot/replay,
/// summary, and lifecycle (Dispose) methods.
/// </summary>
public sealed partial class NetworkStateTracker
{
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

            var result = await _mettaEngine.AddFactAsync(fact, ct).ConfigureAwait(false);
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

            var result = await _mettaEngine.AddFactAsync(rule, ct).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                totalAdded++;
            }
        }

        // Note: Branch export requires the PipelineBranch instance.
        // Use ExportToMeTTaAsync(branch) for individual branches.

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

        var verifyResult = await _mettaEngine.VerifyDagConstraintAsync(branchName, constraint, ct).ConfigureAwait(false);
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
                : Option<PipelineBranchReifier>.None;
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
                await _qdrantStore.DisposeAsync().ConfigureAwait(false);
            }

            _disposed = true;
        }
    }
}

// <copyright file="WorldModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;

namespace Ouroboros.Agent.WorldModel;

using Microsoft.Extensions.Logging;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Embodied;

/// <summary>
/// Memory-based world model using k-nearest neighbor prediction.
/// Stores observed transitions and uses similarity matching for state prediction.
/// </summary>
public sealed partial class WorldModel : IWorldModel
{
    private readonly ILogger<WorldModel>? _logger;
    private readonly List<StateTransition> _transitionMemory;
    private readonly int _maxMemorySize;
    private readonly int _kNeighbors;
    private readonly Dictionary<string, (int predictions, int correct)> _accuracyTracker;
    private readonly object _lockObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldModel"/> class.
    /// </summary>
    /// <param name="maxMemorySize">Maximum number of transitions to store</param>
    /// <param name="kNeighbors">Number of nearest neighbors to use for prediction</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public WorldModel(
        int maxMemorySize = 10000,
        int kNeighbors = 5,
        ILogger<WorldModel>? logger = null)
    {
        if (maxMemorySize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMemorySize), "Must be positive");
        }

        if (kNeighbors <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(kNeighbors), "Must be positive");
        }

        _maxMemorySize = maxMemorySize;
        _kNeighbors = kNeighbors;
        _logger = logger;
        _transitionMemory = new List<StateTransition>();
        _accuracyTracker = new Dictionary<string, (int, int)>();
    }

    /// <inheritdoc/>
    public Task<Result<PredictedState, string>> PredictAsync(
        SensorState currentState,
        EmbodiedAction action,
        CancellationToken ct = default)
    {
        if (currentState == null)
        {
            return Task.FromResult(Result<PredictedState, string>.Failure("Current state cannot be null"));
        }

        if (action == null)
        {
            return Task.FromResult(Result<PredictedState, string>.Failure("Action cannot be null"));
        }

        lock (_lockObject)
        {
            if (_transitionMemory.Count == 0)
            {
                return Task.FromResult(Result<PredictedState, string>.Failure("No experience available for prediction"));
            }

            // Find k-nearest neighbors based on state-action similarity
            var neighbors = FindNearestNeighbors(currentState, action);

            if (neighbors.Count == 0)
            {
                return Task.FromResult(Result<PredictedState, string>.Failure("No similar transitions found"));
            }

            // Aggregate predictions from neighbors
            var predictedState = AggregatePredictions(neighbors, currentState, action);

            var confidence = CalculateConfidence(neighbors);
            var metadata = new Dictionary<string, object>
            {
                ["neighbors_count"] = neighbors.Count,
                ["average_similarity"] = neighbors.Average(n => n.similarity),
            };

            var predicted = new PredictedState(
                predictedState.state,
                predictedState.reward,
                predictedState.terminal,
                confidence,
                metadata);

            return Task.FromResult(Result<PredictedState, string>.Success(predicted));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> UpdateFromExperienceAsync(
        IReadOnlyList<EmbodiedTransition> transitions,
        CancellationToken ct = default)
    {
        if (transitions == null || transitions.Count == 0)
        {
            return Task.FromResult(Result<Unit, string>.Failure("Transitions list cannot be null or empty"));
        }

        try
        {
            lock (_lockObject)
            {
                foreach (var transition in transitions)
                {
                    if (transition == null)
                    {
                        continue;
                    }

                    var stateTransition = new StateTransition(
                        transition.StateBefore,
                        transition.Action,
                        transition.StateAfter,
                        transition.Reward,
                        transition.Terminal,
                        DateTime.UtcNow);

                    _transitionMemory.Add(stateTransition);

                    // Update accuracy tracking
                    UpdateAccuracyMetrics(transition);
                }

                // Maintain memory size limit (FIFO)
                while (_transitionMemory.Count > _maxMemorySize)
                {
                    _transitionMemory.RemoveAt(0);
                }

                _logger?.LogInformation(
                    "Updated world model with {Count} transitions. Total memory: {Total}",
                    transitions.Count,
                    _transitionMemory.Count);
            }

            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update world model from experience");
            return Task.FromResult(Result<Unit, string>.Failure($"Update failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<List<EmbodiedAction>, string>> PlanWithModelAsync(
        SensorState current,
        string goal,
        int horizon = 10,
        CancellationToken ct = default)
    {
        if (current == null)
        {
            return Result<List<EmbodiedAction>, string>.Failure("Current state cannot be null");
        }

        if (string.IsNullOrWhiteSpace(goal))
        {
            return Result<List<EmbodiedAction>, string>.Failure("Goal cannot be null or empty");
        }

        if (horizon <= 0)
        {
            return Result<List<EmbodiedAction>, string>.Failure("Horizon must be positive");
        }

        lock (_lockObject)
        {
            if (_transitionMemory.Count == 0)
            {
                return Result<List<EmbodiedAction>, string>.Failure("No experience available for planning");
            }
        }

        try
        {
            // Beam search with greedy action selection
            var plan = await BeamSearchAsync(current, goal, horizon, ct);

            _logger?.LogInformation(
                "Generated plan with {Count} actions for goal: {Goal}",
                plan.Count,
                goal);

            return Result<List<EmbodiedAction>, string>.Success(plan);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Planning failed for goal: {Goal}", goal);
            return Result<List<EmbodiedAction>, string>.Failure($"Planning failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<double> GetUncertaintyAsync(SensorState state, EmbodiedAction action)
    {
        if (state == null || action == null)
        {
            return Task.FromResult(1.0); // Maximum uncertainty
        }

        lock (_lockObject)
        {
            if (_transitionMemory.Count == 0)
            {
                return Task.FromResult(1.0); // No data = high uncertainty
            }

            var neighbors = FindNearestNeighbors(state, action);

            if (neighbors.Count == 0)
            {
                return Task.FromResult(1.0); // No similar states = high uncertainty
            }

            // Uncertainty based on:
            // 1. Number of neighbors found (more = less uncertain)
            // 2. Average similarity (higher = less uncertain)
            // 3. Variance in predicted outcomes (higher = more uncertain)

            var avgSimilarity = neighbors.Average(n => n.similarity);
            var neighborRatio = Math.Min(1.0, neighbors.Count / (double)_kNeighbors);

            // Calculate outcome variance
            var rewards = neighbors.Select(n => n.transition.Reward).ToList();
            var rewardVariance = rewards.Count > 1 ? CalculateVariance(rewards) : 0.0;
            var normalizedVariance = Math.Min(1.0, rewardVariance / 10.0); // Normalize to 0-1

            // Combine factors: lower similarity, fewer neighbors, higher variance = more uncertainty
            var uncertainty = 1.0 - (avgSimilarity * neighborRatio * (1.0 - normalizedVariance));

            return Task.FromResult(Math.Clamp(uncertainty, 0.0, 1.0));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<List<PredictedState>, string>> SimulateTrajectoryAsync(
        SensorState initialState,
        IReadOnlyList<EmbodiedAction> actions,
        CancellationToken ct = default)
    {
        if (initialState == null)
        {
            return Result<List<PredictedState>, string>.Failure("Initial state cannot be null");
        }

        if (actions == null || actions.Count == 0)
        {
            return Result<List<PredictedState>, string>.Failure("Actions list cannot be null or empty");
        }

        try
        {
            var trajectory = new List<PredictedState>();
            var currentState = initialState;

            foreach (var action in actions)
            {
                ct.ThrowIfCancellationRequested();

                var predictionResult = await PredictAsync(currentState, action, ct);

                if (predictionResult.IsFailure)
                {
                    return Result<List<PredictedState>, string>.Failure(
                        $"Trajectory simulation failed at step {trajectory.Count}: {predictionResult.Error}");
                }

                var predicted = predictionResult.Value;
                trajectory.Add(predicted);

                // Stop if terminal state reached
                if (predicted.Terminal)
                {
                    _logger?.LogInformation(
                        "Trajectory simulation terminated early at step {Step} (terminal state)",
                        trajectory.Count);
                    break;
                }

                currentState = predicted.State;
            }

            return Result<List<PredictedState>, string>.Success(trajectory);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Trajectory simulation failed");
            return Result<List<PredictedState>, string>.Failure($"Simulation failed: {ex.Message}");
        }
    }

    #region Explicit IWorldModel implementation (Abstractions.Domain types)

    // The IWorldModel interface is bound to Ouroboros.Abstractions.Domain placeholder types,
    // while this class operates on the richer Ouroboros.Domain.Embodied types.
    // These explicit implementations satisfy the interface contract.

    /// <inheritdoc />
    Task<Result<Ouroboros.Abstractions.Domain.PredictedState, string>> IWorldModel.PredictAsync(
        Ouroboros.Abstractions.Domain.SensorState currentState,
        Ouroboros.Abstractions.Domain.EmbodiedAction action,
        CancellationToken ct) =>
        Task.FromResult(Result<Ouroboros.Abstractions.Domain.PredictedState, string>.Failure(
            "Use the overload accepting Ouroboros.Domain.Embodied types."));

    /// <inheritdoc />
    Task<Result<Unit, string>> IWorldModel.UpdateFromExperienceAsync(
        IReadOnlyList<Ouroboros.Abstractions.Domain.EmbodiedTransition> transitions,
        CancellationToken ct) =>
        Task.FromResult(Result<Unit, string>.Failure(
            "Use the overload accepting Ouroboros.Domain.Embodied types."));

    /// <inheritdoc />
    Task<Result<List<Ouroboros.Abstractions.Domain.EmbodiedAction>, string>> IWorldModel.PlanWithModelAsync(
        Ouroboros.Abstractions.Domain.SensorState current,
        string goal,
        int horizon,
        CancellationToken ct) =>
        Task.FromResult(Result<List<Ouroboros.Abstractions.Domain.EmbodiedAction>, string>.Failure(
            "Use the overload accepting Ouroboros.Domain.Embodied types."));

    /// <inheritdoc />
    Task<double> IWorldModel.GetUncertaintyAsync(
        Ouroboros.Abstractions.Domain.SensorState state,
        Ouroboros.Abstractions.Domain.EmbodiedAction action) =>
        Task.FromResult(0.0);

    /// <inheritdoc />
    Task<Result<List<Ouroboros.Abstractions.Domain.PredictedState>, string>> IWorldModel.SimulateTrajectoryAsync(
        Ouroboros.Abstractions.Domain.SensorState initialState,
        IReadOnlyList<Ouroboros.Abstractions.Domain.EmbodiedAction> actions,
        CancellationToken ct) =>
        Task.FromResult(Result<List<Ouroboros.Abstractions.Domain.PredictedState>, string>.Failure(
            "Use the overload accepting Ouroboros.Domain.Embodied types."));

    #endregion
}

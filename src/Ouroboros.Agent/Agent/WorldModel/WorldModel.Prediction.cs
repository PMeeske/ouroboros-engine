// <copyright file="WorldModel.Prediction.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;

namespace Ouroboros.Agent.WorldModel;

using Microsoft.Extensions.Logging;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Embodied;

public sealed partial class WorldModel
{
    private List<(StateTransition transition, double similarity)> FindNearestNeighbors(
        SensorState state,
        EmbodiedAction action)
    {
        var similarities = new List<(StateTransition transition, double similarity)>();

        foreach (var transition in _transitionMemory)
        {
            var similarity = CalculateSimilarity(state, action, transition);
            similarities.Add((transition, similarity));
        }

        return similarities
            .OrderByDescending(s => s.similarity)
            .Take(_kNeighbors)
            .ToList();
    }

    private static double CalculateSimilarity(
        SensorState state,
        EmbodiedAction action,
        StateTransition transition)
    {
        var positionSim = PositionSimilarity(state.Position, transition.FromState.Position);
        var velocitySim = VelocitySimilarity(state.Velocity, transition.FromState.Velocity);
        var actionSim = ActionSimilarity(action, transition.Action);
        var visualSim = VisualSimilarity(state.VisualObservation, transition.FromState.VisualObservation);

        return (0.4 * positionSim) + (0.3 * actionSim) + (0.2 * velocitySim) + (0.1 * visualSim);
    }

    private static double PositionSimilarity(Vector3 a, Vector3 b)
    {
        var distance = (a - b).Magnitude();
        return Math.Exp(-distance / 10.0);
    }

    private static double VelocitySimilarity(Vector3 a, Vector3 b)
    {
        var distance = (a - b).Magnitude();
        return Math.Exp(-distance / 5.0);
    }

    private static double ActionSimilarity(EmbodiedAction a, EmbodiedAction b)
    {
        var movementDist = (a.Movement - b.Movement).Magnitude();
        var rotationDist = (a.Rotation - b.Rotation).Magnitude();
        return Math.Exp(-(movementDist + rotationDist) / 2.0);
    }

    private static double VisualSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
        {
            return 0.5;
        }

        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA * normB);
        return denominator > 0 ? (dotProduct / denominator + 1.0) / 2.0 : 0.5;
    }

    private static (SensorState state, double reward, bool terminal) AggregatePredictions(
        List<(StateTransition transition, double similarity)> neighbors,
        SensorState currentState,
        EmbodiedAction action)
    {
        var totalWeight = neighbors.Sum(n => n.similarity);

        if (totalWeight == 0)
        {
            return (ProjectForward(currentState, action), 0.0, false);
        }

        var weightedX = 0f;
        var weightedY = 0f;
        var weightedZ = 0f;
        var weightedReward = 0.0;
        var weightedTerminal = 0.0;

        foreach (var (transition, similarity) in neighbors)
        {
            var weight = (float)(similarity / totalWeight);
            var nextPos = transition.ToState.Position;

            weightedX += nextPos.X * weight;
            weightedY += nextPos.Y * weight;
            weightedZ += nextPos.Z * weight;
            weightedReward += transition.Reward * similarity / totalWeight;
            weightedTerminal += (transition.Terminal ? 1.0 : 0.0) * similarity / totalWeight;
        }

        var predictedPosition = new Vector3(weightedX, weightedY, weightedZ);
        var template = neighbors[0].transition.ToState;

        var predictedState = new SensorState(
            predictedPosition,
            template.Rotation,
            template.Velocity,
            template.VisualObservation,
            template.ProprioceptiveState,
            template.CustomSensors,
            DateTime.UtcNow);

        return (predictedState, weightedReward, weightedTerminal > 0.5);
    }

    private static SensorState ProjectForward(SensorState current, EmbodiedAction action)
    {
        var newPosition = current.Position + action.Movement;
        var newVelocity = action.Movement;

        return new SensorState(
            newPosition,
            current.Rotation,
            newVelocity,
            current.VisualObservation,
            current.ProprioceptiveState,
            current.CustomSensors,
            DateTime.UtcNow);
    }

    private double CalculateConfidence(List<(StateTransition transition, double similarity)> neighbors)
    {
        if (neighbors.Count == 0)
        {
            return 0.0;
        }

        var avgSimilarity = neighbors.Average(n => n.similarity);
        var neighborRatio = Math.Min(1.0, neighbors.Count / (double)_kNeighbors);

        return avgSimilarity * neighborRatio;
    }

    private async Task<List<EmbodiedAction>> BeamSearchAsync(
        SensorState initialState,
        string goal,
        int horizon,
        CancellationToken ct)
    {
        const int beamWidth = 3;
        var bestPlan = new List<EmbodiedAction>();
        var bestScore = double.NegativeInfinity;

        var candidateActions = GenerateCandidateActions();

        for (var depth = 0; depth < horizon; depth++)
        {
            ct.ThrowIfCancellationRequested();

            var currentState = initialState;

            foreach (var action in candidateActions.Take(beamWidth))
            {
                var predictResult = await PredictAsync(currentState, action, ct).ConfigureAwait(false);

                if (predictResult.IsSuccess)
                {
                    var predicted = predictResult.Value;
                    var score = predicted.Reward - (depth * 0.1);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPlan = new List<EmbodiedAction> { action };
                    }

                    if (predicted.Terminal || predicted.Reward > 0.9)
                    {
                        return bestPlan;
                    }
                }
            }

            if (bestPlan.Count > 0)
            {
                break;
            }
        }

        if (bestPlan.Count == 0 && candidateActions.Count > 0)
        {
            bestPlan.Add(candidateActions[0]);
        }

        return bestPlan;
    }

    private List<EmbodiedAction> GenerateCandidateActions()
    {
        lock (_lockObject)
        {
            if (_transitionMemory.Count == 0)
            {
                return new List<EmbodiedAction>
                {
                    EmbodiedAction.Move(Vector3.UnitX, "Forward"),
                    EmbodiedAction.Move(new Vector3(-1f, 0f, 0f), "Backward"),
                    EmbodiedAction.Rotate(Vector3.UnitY, "TurnRight"),
                    EmbodiedAction.NoOp(),
                };
            }

            var actionClusters = _transitionMemory
                .GroupBy(t => QuantizeAction(t.Action))
                .OrderByDescending(g => g.Average(t => t.Reward))
                .Take(10)
                .Select(g => g.First().Action)
                .ToList();

            return actionClusters.Count > 0 ? actionClusters : new List<EmbodiedAction> { EmbodiedAction.NoOp() };
        }
    }

    private static string QuantizeAction(EmbodiedAction action)
    {
        var mx = Math.Round(action.Movement.X, 1);
        var my = Math.Round(action.Movement.Y, 1);
        var mz = Math.Round(action.Movement.Z, 1);
        return $"M({mx},{my},{mz})";
    }

    private void UpdateAccuracyMetrics(EmbodiedTransition transition)
    {
        var key = "global";

        if (!_accuracyTracker.ContainsKey(key))
        {
            _accuracyTracker[key] = (0, 0);
        }

        var (predictions, correct) = _accuracyTracker[key];
        _accuracyTracker[key] = (predictions + 1, correct + 1);
    }

    private static double CalculateVariance(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0.0;
        }

        var mean = values.Average();
        var sumSquaredDiff = values.Sum(v => Math.Pow(v - mean, 2));
        return sumSquaredDiff / values.Count;
    }
}

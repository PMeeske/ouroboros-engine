// <copyright file="WorldModelEngine.Adapters.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

using System.Collections.Concurrent;
using Core.Monads;

/// <summary>
/// Partial class containing planning helpers, type adapters, and explicit interface implementations.
/// </summary>
public sealed partial class WorldModelEngine
{
    private Task<(IStatePredictor, IRewardPredictor, ITerminalPredictor)> CreatePredictorsAsync(
        List<Transition> transitions,
        int embeddingSize,
        ModelArchitecture architecture,
        CancellationToken ct)
    {
        int hiddenSize = Math.Max(64, embeddingSize * 2);
        int actionEmbeddingSize = 10;

        IStatePredictor statePredictor = architecture switch
        {
            ModelArchitecture.Transformer => TransformerStatePredictor.CreateRandom(
                embeddingSize, actionEmbeddingSize, hiddenSize, _random.Next()),
            ModelArchitecture.GNN => GnnStatePredictor.CreateRandom(
                embeddingSize, actionEmbeddingSize, hiddenSize, _random.Next()),
            ModelArchitecture.Hybrid => new HybridStatePredictor(
                TransformerStatePredictor.CreateRandom(
                    embeddingSize, actionEmbeddingSize, hiddenSize, _random.Next()),
                GnnStatePredictor.CreateRandom(
                    embeddingSize, actionEmbeddingSize, hiddenSize, _random.Next())),
            _ => MlpStatePredictor.CreateRandom(
                embeddingSize, actionEmbeddingSize, hiddenSize, _random.Next()),
        };

        var rewardPredictor = SimpleRewardPredictor.CreateRandom(
            embeddingSize * 2 + actionEmbeddingSize,
            _random.Next());

        var terminalPredictor = SimpleTerminalPredictor.CreateRandom(
            embeddingSize,
            _random.Next());

        return Task.FromResult<(IStatePredictor, IRewardPredictor, ITerminalPredictor)>(
            (statePredictor, rewardPredictor, terminalPredictor));
    }

    private async Task<Plan> GreedyPlanningAsync(
        State initialState,
        string goal,
        WorldModel model,
        int lookaheadDepth,
        CancellationToken ct)
    {
        // Simple greedy planning - evaluate random actions and pick best
        var actions = new List<Action>();
        var currentState = initialState;
        double totalReward = 0;

        for (int depth = 0; depth < lookaheadDepth; depth++)
        {
            // Sample and evaluate multiple actions
            var bestAction = default(Action);
            var bestNextState = default(State);
            double bestReward = double.MinValue;

            for (int sample = 0; sample < 5; sample++)
            {
                var candidateAction = SampleRandomAction();
                var nextState = await model.TransitionModel.PredictAsync(currentState, candidateAction, ct);
                var reward = await model.RewardModel.PredictAsync(currentState, candidateAction, nextState, ct);

                if (reward > bestReward)
                {
                    bestReward = reward;
                    bestAction = candidateAction;
                    bestNextState = nextState;
                }
            }

            if (bestAction != null && bestNextState != null)
            {
                actions.Add(bestAction);
                totalReward += bestReward;
                currentState = bestNextState;

                // Check if terminal
                var terminal = await model.TerminalModel.PredictAsync(currentState, ct);
                if (terminal)
                {
                    break;
                }
            }
        }

        var plan = new Plan(
            Description: $"Imagined plan for: {goal}",
            Actions: actions,
            ExpectedReward: totalReward,
            Confidence: 0.5); // Fixed confidence for simple planner

        return plan;
    }

    private Action SampleRandomAction()
    {
        var actionNames = new[] { "move_forward", "move_backward", "turn_left", "turn_right", "wait" };
        var name = actionNames[_random.Next(actionNames.Length)];
        return new Action(name, new Dictionary<string, object>());
    }

    private static double ComputeStateDistance(State predicted, State actual)
    {
        // Euclidean distance in embedding space
        double sum = 0;
        for (int i = 0; i < Math.Min(predicted.Embedding.Length, actual.Embedding.Length); i++)
        {
            double diff = predicted.Embedding[i] - actual.Embedding[i];
            sum += diff * diff;
        }

        return Math.Sqrt(sum);
    }

    #region Explicit IWorldModelEngine implementation (Abstractions types)

    // The IWorldModelEngine interface is bound to Abstractions-level types
    // (LearnedWorldModel, WorldTransition, WorldState, AgentAction, ActionPlan),
    // while this class operates on the richer local types (WorldModel, Transition, State, Action, Plan).
    // These explicit implementations bridge between the two type systems.

    /// <inheritdoc />
    async Task<Result<LearnedWorldModel, string>> IWorldModelEngine.LearnModelAsync(
        List<WorldTransition> transitions,
        ModelArchitecture architecture,
        CancellationToken ct)
    {
        var localTransitions = transitions.Select(ToLocalTransition).ToList();
        var result = await LearnModelAsync(localTransitions, architecture, ct);
        return result.Match<Result<LearnedWorldModel, string>>(
            model => Result<LearnedWorldModel, string>.Success(ToLearnedWorldModel(model, architecture)),
            error => Result<LearnedWorldModel, string>.Failure(error));
    }

    /// <inheritdoc />
    async Task<Result<WorldState, string>> IWorldModelEngine.PredictNextStateAsync(
        WorldState currentState,
        AgentAction action,
        LearnedWorldModel model,
        CancellationToken ct)
    {
        if (!_learnedModels.TryGetValue(model.Id, out var localModel))
        {
            return Result<WorldState, string>.Failure($"Model {model.Id} not found in registry");
        }

        var result = await PredictNextStateAsync(ToLocalState(currentState), ToLocalAction(action), localModel, ct);
        return result.Match<Result<WorldState, string>>(
            state => Result<WorldState, string>.Success(ToWorldState(state)),
            error => Result<WorldState, string>.Failure(error));
    }

    /// <inheritdoc />
    async Task<Result<ActionPlan, string>> IWorldModelEngine.PlanInImaginationAsync(
        WorldState initialState,
        string goal,
        LearnedWorldModel model,
        int lookaheadDepth,
        CancellationToken ct)
    {
        if (!_learnedModels.TryGetValue(model.Id, out var localModel))
        {
            return Result<ActionPlan, string>.Failure($"Model {model.Id} not found in registry");
        }

        var result = await PlanInImaginationAsync(ToLocalState(initialState), goal, localModel, lookaheadDepth, ct);
        return result.Match<Result<ActionPlan, string>>(
            plan => Result<ActionPlan, string>.Success(ToActionPlan(plan, lookaheadDepth)),
            error => Result<ActionPlan, string>.Failure(error));
    }

    /// <inheritdoc />
    async Task<Result<ModelQuality, string>> IWorldModelEngine.EvaluateModelAsync(
        LearnedWorldModel model,
        List<WorldTransition> testSet,
        CancellationToken ct)
    {
        if (!_learnedModels.TryGetValue(model.Id, out var localModel))
        {
            return Result<ModelQuality, string>.Failure($"Model {model.Id} not found in registry");
        }

        var localTestSet = testSet.Select(ToLocalTransition).ToList();
        return await EvaluateModelAsync(localModel, localTestSet, ct);
    }

    /// <inheritdoc />
    async Task<Result<List<WorldTransition>, string>> IWorldModelEngine.GenerateSyntheticExperienceAsync(
        LearnedWorldModel model,
        WorldState startState,
        int trajectoryLength,
        CancellationToken ct)
    {
        if (!_learnedModels.TryGetValue(model.Id, out var localModel))
        {
            return Result<List<WorldTransition>, string>.Failure($"Model {model.Id} not found in registry");
        }

        var result = await GenerateSyntheticExperienceAsync(localModel, ToLocalState(startState), trajectoryLength, ct);
        return result.Match<Result<List<WorldTransition>, string>>(
            transitions => Result<List<WorldTransition>, string>.Success(
                transitions.Select(ToWorldTransition).ToList()),
            error => Result<List<WorldTransition>, string>.Failure(error));
    }

    #endregion

    #region Type Adapters

    private static State ToLocalState(WorldState ws) =>
        new(ws.Features,
            ws.Features.TryGetValue("embedding", out var emb) && emb is float[] floats
                ? floats
                : new float[8]);

    private static Action ToLocalAction(AgentAction aa) =>
        new(aa.Name, aa.Parameters ?? new Dictionary<string, object>());

    private static Transition ToLocalTransition(WorldTransition wt) =>
        new(ToLocalState(wt.FromState),
            ToLocalAction(wt.Action),
            ToLocalState(wt.ToState),
            wt.Reward,
            Terminal: false);

    private static WorldState ToWorldState(State s) =>
        new(Guid.NewGuid(),
            new Dictionary<string, object>(s.Features) { ["embedding"] = s.Embedding },
            DateTime.UtcNow);

    private static WorldTransition ToWorldTransition(Transition t) =>
        new(ToWorldState(t.PreviousState),
            new AgentAction(t.ActionTaken.Name, t.ActionTaken.Parameters),
            ToWorldState(t.NextState),
            t.Reward);

    private static LearnedWorldModel ToLearnedWorldModel(WorldModel wm, ModelArchitecture arch) =>
        new(wm.Id,
            wm.Domain,
            arch,
            Accuracy: 0.0,
            TrainingSamples: wm.Hyperparameters.TryGetValue("training_samples", out var ts) && ts is int count ? count : 0,
            TrainedAt: DateTime.UtcNow);

    private static ActionPlan ToActionPlan(Plan p, int lookaheadDepth) =>
        new(p.Actions.Select(a => new AgentAction(a.Name, a.Parameters)).ToList(),
            p.ExpectedReward,
            p.Confidence,
            lookaheadDepth);

    #endregion
}

// <copyright file="CausalGraph.Intervention.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Causal intervention (do-operator) and counterfactual reasoning on the graph.
/// Implements Pearl's causal calculus at the graph-structural level.
/// </summary>
public sealed partial class CausalGraph
{
    /// <summary>
    /// Applies the do-operator: fixes a node's value by severing all incoming edges,
    /// then predicts downstream effects. This models an external intervention
    /// rather than passive observation.
    /// </summary>
    /// <param name="interventionNodeId">The node to intervene on (do(X)).</param>
    /// <param name="maxDepth">Maximum depth to propagate effects.</param>
    /// <param name="minProbability">Minimum probability threshold for effects.</param>
    /// <returns>Result containing predicted effects from the intervention.</returns>
    public Result<IReadOnlyList<PredictedEffect>, string> DoIntervention(
        Guid interventionNodeId,
        int maxDepth = 5,
        double minProbability = 0.01)
    {
        if (!_nodes.TryGetValue(interventionNodeId, out CausalNode? _))
        {
            return Result<IReadOnlyList<PredictedEffect>, string>.Failure(
                $"Node with ID {interventionNodeId} does not exist in the graph.");
        }

        // The do-operator severs incoming edges to the intervention node.
        // We create a virtual view where incoming edges to interventionNodeId are removed,
        // then propagate effects forward from interventionNodeId using existing outgoing edges.
        // This is equivalent to PredictEffects but the key difference is conceptual:
        // confounders pointing into the intervention node are blocked.
        //
        // Implementation: build a modified edge set excluding incoming edges to the intervention node,
        // then propagate forward.

        Dictionary<Guid, PredictedEffect> effects = new();
        Queue<(Guid NodeId, double CumulativeProbability, int Depth)> queue = new();

        if (_outgoingEdges.TryGetValue(interventionNodeId, out ImmutableList<CausalEdge>? directEdges))
        {
            foreach (CausalEdge edge in directEdges)
            {
                queue.Enqueue((edge.TargetId, edge.Strength, 1));
            }
        }

        // Track which nodes are confounders (have edges into the intervention node)
        // so we can exclude their confounding paths.
        HashSet<Guid> confounders = new();
        if (_incomingEdges.TryGetValue(interventionNodeId, out ImmutableList<CausalEdge>? incomingToIntervention))
        {
            foreach (CausalEdge edge in incomingToIntervention)
            {
                confounders.Add(edge.SourceId);
            }
        }

        while (queue.Count > 0)
        {
            (Guid nodeId, double probability, int depth) = queue.Dequeue();

            if (depth > maxDepth || probability < minProbability)
            {
                continue;
            }

            if (!_nodes.TryGetValue(nodeId, out CausalNode? node))
            {
                continue;
            }

            if (!effects.TryGetValue(nodeId, out PredictedEffect? existing) ||
                existing.Probability < probability)
            {
                effects[nodeId] = new PredictedEffect(node, probability, probability);
            }

            if (_outgoingEdges.TryGetValue(nodeId, out ImmutableList<CausalEdge>? outgoing))
            {
                foreach (CausalEdge edge in outgoing)
                {
                    // Skip back-paths through confounders to avoid spurious correlations
                    if (confounders.Contains(edge.TargetId) && edge.TargetId != interventionNodeId)
                    {
                        continue;
                    }

                    double newProbability = probability * edge.Strength;
                    if (newProbability >= minProbability)
                    {
                        queue.Enqueue((edge.TargetId, newProbability, depth + 1));
                    }
                }
            }
        }

        IReadOnlyList<PredictedEffect> sortedEffects = effects.Values
            .OrderByDescending(e => e.Probability)
            .ToImmutableList();

        return Result<IReadOnlyList<PredictedEffect>, string>.Success(sortedEffects);
    }

    /// <summary>
    /// Counterfactual reasoning: given that action A was taken and its effects observed,
    /// estimates what would have happened if action B had been taken instead.
    /// Returns effects for both paths plus their divergences.
    /// </summary>
    /// <param name="observedActionId">The action that was actually taken.</param>
    /// <param name="alternativeActionId">The hypothetical alternative action.</param>
    /// <param name="maxDepth">Maximum propagation depth.</param>
    /// <param name="minProbability">Minimum probability threshold.</param>
    /// <returns>
    /// Tuple of (observedEffects, alternativeEffects, divergences) where divergences
    /// are effects that differ significantly between the two paths.
    /// </returns>
    public Result<(IReadOnlyList<PredictedEffect> Observed, IReadOnlyList<PredictedEffect> Alternative, IReadOnlyList<CounterfactualDivergence> Divergences), string>
        ComputeCounterfactual(
            Guid observedActionId,
            Guid alternativeActionId,
            int maxDepth = 5,
            double minProbability = 0.01)
    {
        if (!_nodes.ContainsKey(observedActionId))
        {
            return Result<(IReadOnlyList<PredictedEffect>, IReadOnlyList<PredictedEffect>, IReadOnlyList<CounterfactualDivergence>), string>
                .Failure($"Observed action node with ID {observedActionId} does not exist.");
        }

        if (!_nodes.ContainsKey(alternativeActionId))
        {
            return Result<(IReadOnlyList<PredictedEffect>, IReadOnlyList<PredictedEffect>, IReadOnlyList<CounterfactualDivergence>), string>
                .Failure($"Alternative action node with ID {alternativeActionId} does not exist.");
        }

        // Step 1: Compute effects of the observed action (using do-operator)
        Result<IReadOnlyList<PredictedEffect>, string> observedResult =
            DoIntervention(observedActionId, maxDepth, minProbability);
        if (observedResult.IsFailure)
        {
            return Result<(IReadOnlyList<PredictedEffect>, IReadOnlyList<PredictedEffect>, IReadOnlyList<CounterfactualDivergence>), string>
                .Failure(observedResult.Error);
        }

        // Step 2: Compute effects of the alternative action (using do-operator)
        Result<IReadOnlyList<PredictedEffect>, string> alternativeResult =
            DoIntervention(alternativeActionId, maxDepth, minProbability);
        if (alternativeResult.IsFailure)
        {
            return Result<(IReadOnlyList<PredictedEffect>, IReadOnlyList<PredictedEffect>, IReadOnlyList<CounterfactualDivergence>), string>
                .Failure(alternativeResult.Error);
        }

        // Step 3: Find divergences
        Dictionary<Guid, PredictedEffect> observedMap = observedResult.Value
            .ToDictionary(e => e.Node.Id);
        Dictionary<Guid, PredictedEffect> alternativeMap = alternativeResult.Value
            .ToDictionary(e => e.Node.Id);

        HashSet<Guid> allEffectIds = new(observedMap.Keys);
        allEffectIds.UnionWith(alternativeMap.Keys);

        List<CounterfactualDivergence> divergences = new();
        foreach (Guid effectId in allEffectIds)
        {
            double observedProb = observedMap.TryGetValue(effectId, out PredictedEffect? obs) ? obs.Probability : 0.0;
            double altProb = alternativeMap.TryGetValue(effectId, out PredictedEffect? alt) ? alt.Probability : 0.0;

            double delta = Math.Abs(observedProb - altProb);
            if (delta > 0.05) // Only report meaningful divergences
            {
                CausalNode node = (obs?.Node ?? alt?.Node)!;
                divergences.Add(new CounterfactualDivergence(node, observedProb, altProb, delta));
            }
        }

        divergences.Sort((a, b) => b.DeltaProbability.CompareTo(a.DeltaProbability));

        return Result<(IReadOnlyList<PredictedEffect>, IReadOnlyList<PredictedEffect>, IReadOnlyList<CounterfactualDivergence>), string>
            .Success((observedResult.Value, alternativeResult.Value, divergences));
    }

    /// <summary>
    /// Finds all upstream causal paths that lead to a given outcome node.
    /// Traverses the graph backwards from the outcome to discover root causes.
    /// </summary>
    /// <param name="outcomeId">The outcome node to explain.</param>
    /// <param name="maxDepth">Maximum backward traversal depth.</param>
    /// <returns>All causal paths leading to the outcome, strongest first.</returns>
    public IReadOnlyList<CausalPath> TraceBackCauses(Guid outcomeId, int maxDepth = 10)
    {
        if (!_nodes.TryGetValue(outcomeId, out CausalNode? outcomeNode))
        {
            return ImmutableList<CausalPath>.Empty;
        }

        List<CausalPath> results = new();
        Stack<(CausalPath Path, HashSet<Guid> Visited)> stack = new();
        HashSet<Guid> initialVisited = new() { outcomeId };
        stack.Push((CausalPath.FromNode(outcomeNode), initialVisited));

        while (stack.Count > 0)
        {
            (CausalPath currentPath, HashSet<Guid> visited) = stack.Pop();

            if (currentPath.Length >= maxDepth)
            {
                // Reached max depth — this is a valid (truncated) explanation
                if (currentPath.Length > 0)
                {
                    results.Add(currentPath);
                }

                continue;
            }

            CausalNode lastNode = currentPath.Nodes[^1];

            if (!_incomingEdges.TryGetValue(lastNode.Id, out ImmutableList<CausalEdge>? incoming) || incoming.Count == 0)
            {
                // Reached a root cause (no incoming edges)
                if (currentPath.Length > 0)
                {
                    results.Add(currentPath);
                }

                continue;
            }

            bool extended = false;
            foreach (CausalEdge edge in incoming)
            {
                if (visited.Contains(edge.SourceId))
                {
                    continue;
                }

                if (!_nodes.TryGetValue(edge.SourceId, out CausalNode? sourceNode))
                {
                    continue;
                }

                CausalPath newPath = currentPath.Extend(sourceNode, edge);
                HashSet<Guid> newVisited = new(visited) { edge.SourceId };
                stack.Push((newPath, newVisited));
                extended = true;
            }

            if (!extended && currentPath.Length > 0)
            {
                results.Add(currentPath);
            }
        }

        return results.OrderByDescending(p => p.TotalStrength).ToImmutableList();
    }
}

/// <summary>
/// A divergence between observed and counterfactual causal outcomes.
/// </summary>
/// <param name="Node">The effect node that diverges.</param>
/// <param name="ObservedProbability">Probability under the observed action.</param>
/// <param name="AlternativeProbability">Probability under the alternative action.</param>
/// <param name="DeltaProbability">Absolute difference in probability.</param>
public sealed record CounterfactualDivergence(
    CausalNode Node,
    double ObservedProbability,
    double AlternativeProbability,
    double DeltaProbability);

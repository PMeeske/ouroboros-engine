// <copyright file="CausalReasoner.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.WorldModel;

using Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Maintains a living causal DAG that grows from runtime observations.
/// Supports Pearl's do-operator for interventional queries and
/// counterfactual reasoning for "what-if" reflection.
/// </summary>
public sealed class CausalReasoner : ICausalReasoner
{
    private readonly object _lock = new();
    private CausalGraph _graph;

    /// <summary>Node name → node ID lookup for fast name-based queries.</summary>
    private readonly Dictionary<string, Guid> _nameIndex = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Edge key (causeId, effectId) → running EMA strength.</summary>
    private readonly Dictionary<(Guid, Guid), double> _edgeStrengths = new();

    /// <summary>Observation count per edge for EMA weighting.</summary>
    private readonly Dictionary<(Guid, Guid), int> _edgeObservationCounts = new();

    /// <summary>EMA smoothing factor (higher = more weight on recent observations).</summary>
    private const double EmaSmoothingFactor = 0.3;

    /// <summary>
    /// Initializes a new instance of the <see cref="CausalReasoner"/> class with an empty graph.
    /// </summary>
    public CausalReasoner()
    {
        _graph = CausalGraph.Empty();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CausalReasoner"/> class
    /// with an existing causal graph.
    /// </summary>
    /// <param name="initialGraph">The initial graph to reason over.</param>
    public CausalReasoner(CausalGraph initialGraph)
    {
        ArgumentNullException.ThrowIfNull(initialGraph);
        _graph = initialGraph;

        // Build name index from existing nodes
        foreach (CausalNode node in initialGraph.Nodes)
        {
            _nameIndex[node.Name] = node.Id;
        }
    }

    /// <inheritdoc/>
    public int NodeCount
    {
        get
        {
            lock (_lock) { return _graph.NodeCount; }
        }
    }

    /// <inheritdoc/>
    public int EdgeCount
    {
        get
        {
            lock (_lock) { return _graph.EdgeCount; }
        }
    }

    /// <summary>
    /// Gets a snapshot of the current causal graph (for visualization or export).
    /// </summary>
    public CausalGraph GetGraphSnapshot()
    {
        lock (_lock) { return _graph; }
    }

    /// <inheritdoc/>
    public void ObserveCause(
        string causeName,
        string effectName,
        double strength,
        CausalNodeKind causeType = CausalNodeKind.Action,
        CausalNodeKind effectType = CausalNodeKind.State)
    {
        ArgumentNullException.ThrowIfNull(causeName);
        ArgumentNullException.ThrowIfNull(effectName);
        double clampedStrength = Math.Clamp(strength, 0.0, 1.0);

        lock (_lock)
        {
            Guid causeId = EnsureNode(causeName, MapNodeKind(causeType));
            Guid effectId = EnsureNode(effectName, MapNodeKind(effectType));

            var edgeKey = (causeId, effectId);

            if (_edgeStrengths.TryGetValue(edgeKey, out double currentStrength))
            {
                // Update via EMA
                int count = _edgeObservationCounts.GetValueOrDefault(edgeKey, 1);
                double alpha = Math.Min(EmaSmoothingFactor, 2.0 / (count + 1));
                double newStrength = (alpha * clampedStrength) + ((1 - alpha) * currentStrength);
                _edgeStrengths[edgeKey] = Math.Clamp(newStrength, 0.0, 1.0);
                _edgeObservationCounts[edgeKey] = count + 1;

                // Rebuild edge in graph (remove old, add new)
                RebuildEdge(causeId, effectId, _edgeStrengths[edgeKey]);
            }
            else
            {
                // New edge
                _edgeStrengths[edgeKey] = clampedStrength;
                _edgeObservationCounts[edgeKey] = 1;

                CausalEdge edge = CausalEdge.Create(causeId, effectId, clampedStrength);
                Result<CausalGraph, string> result = _graph.AddEdge(edge);
                if (result.IsSuccess)
                {
                    _graph = result.Value;
                }
            }
        }
    }

    /// <inheritdoc/>
    public Result<InterventionResult, string> DoIntervention(string actionName, int maxDepth = 5)
    {
        ArgumentNullException.ThrowIfNull(actionName);

        lock (_lock)
        {
            if (!_nameIndex.TryGetValue(actionName, out Guid nodeId))
            {
                return Result<InterventionResult, string>.Failure(
                    $"No causal node named '{actionName}' exists in the graph.");
            }

            Result<IReadOnlyList<PredictedEffect>, string> graphResult =
                _graph.DoIntervention(nodeId, maxDepth);

            if (graphResult.IsFailure)
            {
                return Result<InterventionResult, string>.Failure(graphResult.Error);
            }

            IReadOnlyList<PredictedEffect> effects = graphResult.Value;

            List<PredictedCausalEffect> mappedEffects = new(effects.Count);
            foreach (PredictedEffect effect in effects)
            {
                // Estimate depth from probability attenuation
                int estimatedDepth = EstimateDepth(nodeId, effect.Node.Id);
                mappedEffects.Add(new PredictedCausalEffect(
                    effect.Node.Name,
                    effect.Probability,
                    estimatedDepth));
            }

            return Result<InterventionResult, string>.Success(
                new InterventionResult(actionName, mappedEffects, effects.Count));
        }
    }

    /// <inheritdoc/>
    public Result<CounterfactualResult, string> Counterfactual(
        string observedActionName,
        string alternativeActionName,
        int maxDepth = 5)
    {
        ArgumentNullException.ThrowIfNull(observedActionName);
        ArgumentNullException.ThrowIfNull(alternativeActionName);

        lock (_lock)
        {
            if (!_nameIndex.TryGetValue(observedActionName, out Guid observedId))
            {
                return Result<CounterfactualResult, string>.Failure(
                    $"No causal node named '{observedActionName}' exists in the graph.");
            }

            if (!_nameIndex.TryGetValue(alternativeActionName, out Guid altId))
            {
                return Result<CounterfactualResult, string>.Failure(
                    $"No causal node named '{alternativeActionName}' exists in the graph.");
            }

            var graphResult = _graph.ComputeCounterfactual(observedId, altId, maxDepth);

            if (graphResult.IsFailure)
            {
                return Result<CounterfactualResult, string>.Failure(graphResult.Error);
            }

            var (observed, alternative, divergences) = graphResult.Value;

            List<PredictedCausalEffect> observedEffects = MapEffects(observedId, observed);
            List<PredictedCausalEffect> altEffects = MapEffects(altId, alternative);

            List<CausalDivergence> mappedDivergences = new(divergences.Count);
            foreach (CounterfactualDivergence div in divergences)
            {
                mappedDivergences.Add(new CausalDivergence(
                    div.Node.Name,
                    div.ObservedProbability,
                    div.AlternativeProbability));
            }

            return Result<CounterfactualResult, string>.Success(
                new CounterfactualResult(
                    observedActionName,
                    alternativeActionName,
                    observedEffects,
                    altEffects,
                    mappedDivergences));
        }
    }

    /// <inheritdoc/>
    public Result<IReadOnlyList<CausalExplanation>, string> ExplainCauses(
        string outcomeName,
        int maxPaths = 5)
    {
        ArgumentNullException.ThrowIfNull(outcomeName);

        lock (_lock)
        {
            if (!_nameIndex.TryGetValue(outcomeName, out Guid outcomeId))
            {
                return Result<IReadOnlyList<CausalExplanation>, string>.Failure(
                    $"No causal node named '{outcomeName}' exists in the graph.");
            }

            IReadOnlyList<CausalPath> paths = _graph.TraceBackCauses(outcomeId, maxDepth: 10);

            List<CausalExplanation> explanations = new();
            foreach (CausalPath path in paths.Take(maxPaths))
            {
                // Path is from outcome backwards to root cause — reverse for human-readable order
                List<string> causeChain = path.Nodes
                    .Select(n => n.Name)
                    .Reverse()
                    .ToList();

                explanations.Add(new CausalExplanation(
                    outcomeName,
                    causeChain,
                    path.TotalStrength));
            }

            return Result<IReadOnlyList<CausalExplanation>, string>.Success(explanations);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ensures a node exists in the graph, creating it if needed.
    /// Returns the node ID.
    /// </summary>
    private Guid EnsureNode(string name, CausalNodeType nodeType)
    {
        if (_nameIndex.TryGetValue(name, out Guid existingId))
        {
            return existingId;
        }

        CausalNode node = CausalNode.Create(name, name, nodeType);
        Result<CausalGraph, string> result = _graph.AddNode(node);
        if (result.IsSuccess)
        {
            _graph = result.Value;
        }

        _nameIndex[name] = node.Id;
        return node.Id;
    }

    /// <summary>
    /// Rebuilds an edge with an updated strength by removing the old graph
    /// and re-creating it with the updated edge. Since CausalGraph is immutable,
    /// we reconstruct from the current nodes and updated edges.
    /// </summary>
    private void RebuildEdge(Guid sourceId, Guid targetId, double newStrength)
    {
        // Collect all edges, replacing the one that matches
        List<CausalEdge> updatedEdges = new();
        bool replaced = false;

        foreach (CausalEdge edge in _graph.Edges)
        {
            if (edge.SourceId == sourceId && edge.TargetId == targetId && !replaced)
            {
                updatedEdges.Add(CausalEdge.Create(sourceId, targetId, newStrength));
                replaced = true;
            }
            else
            {
                updatedEdges.Add(edge);
            }
        }

        Result<CausalGraph, string> result = CausalGraph.Create(_graph.Nodes, updatedEdges);
        if (result.IsSuccess)
        {
            _graph = result.Value;
        }
    }

    /// <summary>
    /// Maps CausalNodeKind (abstraction layer) to CausalNodeType (pipeline layer).
    /// </summary>
    private static CausalNodeType MapNodeKind(CausalNodeKind kind)
    {
        return kind switch
        {
            CausalNodeKind.State => CausalNodeType.State,
            CausalNodeKind.Action => CausalNodeType.Action,
            CausalNodeKind.Event => CausalNodeType.Event,
            _ => CausalNodeType.Event,
        };
    }

    /// <summary>
    /// Estimates depth from source to target using shortest path.
    /// </summary>
    private int EstimateDepth(Guid fromId, Guid toId)
    {
        Option<CausalPath> path = _graph.FindPath(fromId, toId);
        return path.HasValue ? path.Value.Length : 1;
    }

    /// <summary>
    /// Maps PredictedEffect list to PredictedCausalEffect list.
    /// </summary>
    private List<PredictedCausalEffect> MapEffects(Guid sourceId, IReadOnlyList<PredictedEffect> effects)
    {
        List<PredictedCausalEffect> mapped = new(effects.Count);
        foreach (PredictedEffect effect in effects)
        {
            int depth = EstimateDepth(sourceId, effect.Node.Id);
            mapped.Add(new PredictedCausalEffect(effect.Node.Name, effect.Probability, depth));
        }

        return mapped;
    }
}

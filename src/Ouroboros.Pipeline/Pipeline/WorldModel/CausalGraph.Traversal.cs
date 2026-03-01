// <copyright file="CausalGraph.Traversal.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.WorldModel;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;

public sealed partial class CausalGraph
{
    /// <summary>
    /// Predicts the effects of taking an action, returning affected nodes with their probabilities.
    /// </summary>
    public Result<IReadOnlyList<PredictedEffect>, string> PredictEffects(
        Guid actionNodeId,
        int maxDepth = 5,
        double minProbability = 0.01)
    {
        if (!_nodes.TryGetValue(actionNodeId, out CausalNode? _))
        {
            return Result<IReadOnlyList<PredictedEffect>, string>.Failure(
                $"Node with ID {actionNodeId} does not exist in the graph.");
        }

        Dictionary<Guid, PredictedEffect> effects = new();
        Queue<(Guid NodeId, double CumulativeProbability, int Depth)> queue = new();

        if (_outgoingEdges.TryGetValue(actionNodeId, out ImmutableList<CausalEdge>? directEdges))
        {
            foreach (CausalEdge edge in directEdges)
            {
                queue.Enqueue((edge.TargetId, edge.Strength, 1));
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
    /// Finds the shortest causal path between two nodes using breadth-first search.
    /// </summary>
    public Option<CausalPath> FindPath(Guid fromId, Guid toId)
    {
        if (!_nodes.TryGetValue(fromId, out CausalNode? fromNode) ||
            !_nodes.TryGetValue(toId, out CausalNode? _))
        {
            return Option<CausalPath>.None();
        }

        if (fromId == toId)
        {
            return Option<CausalPath>.Some(CausalPath.FromNode(fromNode));
        }

        Queue<CausalPath> queue = new();
        HashSet<Guid> visited = new() { fromId };

        queue.Enqueue(CausalPath.FromNode(fromNode));

        while (queue.Count > 0)
        {
            CausalPath currentPath = queue.Dequeue();
            CausalNode lastNode = currentPath.Nodes[^1];

            if (!_outgoingEdges.TryGetValue(lastNode.Id, out ImmutableList<CausalEdge>? outgoing))
            {
                continue;
            }

            foreach (CausalEdge edge in outgoing)
            {
                if (visited.Contains(edge.TargetId))
                {
                    continue;
                }

                if (!_nodes.TryGetValue(edge.TargetId, out CausalNode? targetNode))
                {
                    continue;
                }

                CausalPath newPath = currentPath.Extend(targetNode, edge);

                if (edge.TargetId == toId)
                {
                    return Option<CausalPath>.Some(newPath);
                }

                visited.Add(edge.TargetId);
                queue.Enqueue(newPath);
            }
        }

        return Option<CausalPath>.None();
    }

    /// <summary>
    /// Finds all causal paths between two nodes up to a maximum depth.
    /// </summary>
    public IReadOnlyList<CausalPath> FindAllPaths(Guid fromId, Guid toId, int maxDepth = 10)
    {
        List<CausalPath> results = new();

        if (!_nodes.TryGetValue(fromId, out CausalNode? fromNode) ||
            !_nodes.TryGetValue(toId, out CausalNode? _))
        {
            return results;
        }

        if (fromId == toId)
        {
            results.Add(CausalPath.FromNode(fromNode));
            return results;
        }

        Stack<(CausalPath Path, HashSet<Guid> Visited)> stack = new();
        HashSet<Guid> initialVisited = new() { fromId };
        stack.Push((CausalPath.FromNode(fromNode), initialVisited));

        while (stack.Count > 0)
        {
            (CausalPath currentPath, HashSet<Guid> visited) = stack.Pop();

            if (currentPath.Length >= maxDepth)
            {
                continue;
            }

            CausalNode lastNode = currentPath.Nodes[^1];

            if (!_outgoingEdges.TryGetValue(lastNode.Id, out ImmutableList<CausalEdge>? outgoing))
            {
                continue;
            }

            foreach (CausalEdge edge in outgoing)
            {
                if (visited.Contains(edge.TargetId))
                {
                    continue;
                }

                if (!_nodes.TryGetValue(edge.TargetId, out CausalNode? targetNode))
                {
                    continue;
                }

                CausalPath newPath = currentPath.Extend(targetNode, edge);

                if (edge.TargetId == toId)
                {
                    results.Add(newPath);
                    continue;
                }

                HashSet<Guid> newVisited = new(visited) { edge.TargetId };
                stack.Push((newPath, newVisited));
            }
        }

        return results.OrderByDescending(p => p.TotalStrength).ToImmutableList();
    }

    /// <summary>
    /// Calculates the total causal strength from one node to another.
    /// </summary>
    public double CalculateTotalCausalStrength(Guid fromId, Guid toId, int maxDepth = 10)
    {
        IReadOnlyList<CausalPath> paths = FindAllPaths(fromId, toId, maxDepth);

        if (paths.Count == 0)
        {
            return 0.0;
        }

        double probabilityNone = 1.0;
        foreach (CausalPath path in paths)
        {
            probabilityNone *= (1.0 - path.TotalStrength);
        }

        return 1.0 - probabilityNone;
    }

    /// <summary>
    /// Creates a subgraph containing only the specified nodes and edges between them.
    /// </summary>
    public Result<CausalGraph, string> CreateSubgraph(IEnumerable<Guid> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);

        HashSet<Guid> nodeIdSet = nodeIds.ToHashSet();
        List<CausalNode> subgraphNodes = new();
        List<CausalEdge> subgraphEdges = new();

        foreach (Guid nodeId in nodeIdSet)
        {
            if (_nodes.TryGetValue(nodeId, out CausalNode? node))
            {
                subgraphNodes.Add(node);
            }
            else
            {
                return Result<CausalGraph, string>.Failure($"Node with ID {nodeId} does not exist in the graph.");
            }
        }

        subgraphEdges.AddRange(_edges.Where(edge => nodeIdSet.Contains(edge.SourceId) && nodeIdSet.Contains(edge.TargetId)));

        return Create(subgraphNodes, subgraphEdges);
    }

    /// <summary>
    /// Helper method for cycle detection using DFS.
    /// </summary>
    private bool HasCycleUtil(Guid nodeId, HashSet<Guid> visited, HashSet<Guid> recursionStack)
    {
        if (recursionStack.Contains(nodeId))
        {
            return true;
        }

        if (visited.Contains(nodeId))
        {
            return false;
        }

        visited.Add(nodeId);
        recursionStack.Add(nodeId);

        if (_outgoingEdges.TryGetValue(nodeId, out ImmutableList<CausalEdge>? outgoing)
            && outgoing.Any(edge => HasCycleUtil(edge.TargetId, visited, recursionStack)))
        {
            return true;
        }

        recursionStack.Remove(nodeId);
        return false;
    }
}

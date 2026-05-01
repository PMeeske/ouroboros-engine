// <copyright file="DiscreteGeodesicReasoner.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Riemannian;

/// <summary>
/// Computes shortest paths on a <see cref="LocalNeighborhoodGraph"/> as
/// a discrete approximation of Riemannian geodesics.
/// </summary>
/// <remarks>
/// <para>
/// Implements Dijkstra's algorithm with a binary-heap priority queue
/// (<see cref="PriorityQueue{TElement, TPriority}"/>). All edge weights
/// must be non-negative — the graph enforces this invariant on insertion.
/// </para>
/// <para>
/// The reasoner satisfies the geodesic metric axioms relative to the
/// underlying graph weights:
/// <list type="bullet">
///   <item>Non-negativity: cost(a,b) &gt;= 0.</item>
///   <item>Identity: cost(a,a) = 0.</item>
///   <item>Symmetry: cost(a,b) = cost(b,a) (graph is undirected).</item>
///   <item>Triangle inequality: cost(a,c) &lt;= cost(a,b) + cost(b,c).</item>
/// </list>
/// </para>
/// </remarks>
public sealed class DiscreteGeodesicReasoner
{
    /// <summary>
    /// Computes the shortest path from <paramref name="source"/> to
    /// <paramref name="target"/> on <paramref name="graph"/>.
    /// </summary>
    /// <param name="graph">The graph to traverse.</param>
    /// <param name="source">Start node.</param>
    /// <param name="target">End node.</param>
    /// <returns>A geodesic result; <see cref="DiscreteGeodesicResult.IsValid"/> reflects path existence.</returns>
    public DiscreteGeodesicResult ComputeShortestPath(
        LocalNeighborhoodGraph graph,
        NodeId source,
        NodeId target)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (!graph.Contains(source) || !graph.Contains(target))
        {
            return Empty(source, target);
        }

        if (source.Equals(target))
        {
            return new DiscreteGeodesicResult(
                Source: source,
                Target: target,
                PathNodes: new[] { source },
                SegmentCosts: Array.Empty<float>(),
                ComputedPathCost: 0f,
                CurvatureProxy: 0f,
                IsValid: true);
        }

        // Dijkstra.
        Dictionary<NodeId, float> distances = new();
        Dictionary<NodeId, NodeId> previous = new();
        HashSet<NodeId> settled = new();

        PriorityQueue<NodeId, float> queue = new();
        distances[source] = 0f;
        queue.Enqueue(source, 0f);

        while (queue.Count > 0)
        {
            NodeId current = queue.Dequeue();
            if (!settled.Add(current))
            {
                continue;
            }

            if (current.Equals(target))
            {
                break;
            }

            float currentDist = distances[current];
            foreach (var (neighbor, weight) in graph.Neighbors(current))
            {
                if (settled.Contains(neighbor))
                {
                    continue;
                }

                float candidate = currentDist + weight;
                if (!distances.TryGetValue(neighbor, out float existing) || candidate < existing)
                {
                    distances[neighbor] = candidate;
                    previous[neighbor] = current;
                    queue.Enqueue(neighbor, candidate);
                }
            }
        }

        if (!distances.ContainsKey(target))
        {
            return Empty(source, target);
        }

        // Reconstruct path target -> source.
        List<NodeId> reversed = new() { target };
        NodeId cursor = target;
        while (previous.TryGetValue(cursor, out NodeId parent))
        {
            reversed.Add(parent);
            cursor = parent;
            if (cursor.Equals(source))
            {
                break;
            }
        }

        if (!cursor.Equals(source))
        {
            return Empty(source, target);
        }

        reversed.Reverse();
        IReadOnlyList<NodeId> pathNodes = reversed;

        // Per-segment costs come from the graph's adjacency (not the
        // dijkstra distances) so we faithfully reflect the edge weights.
        List<float> segmentCosts = new(pathNodes.Count - 1);
        for (int i = 0; i + 1 < pathNodes.Count; i++)
        {
            float segCost = LookupEdge(graph, pathNodes[i], pathNodes[i + 1]);
            segmentCosts.Add(segCost);
        }

        float totalCost = 0f;
        foreach (float c in segmentCosts)
        {
            totalCost += c;
        }

        float curvatureProxy = ComputeCurvatureProxy(segmentCosts);

        return new DiscreteGeodesicResult(
            Source: source,
            Target: target,
            PathNodes: pathNodes,
            SegmentCosts: segmentCosts,
            ComputedPathCost: totalCost,
            CurvatureProxy: curvatureProxy,
            IsValid: true);
    }

    private static DiscreteGeodesicResult Empty(NodeId source, NodeId target) =>
        new(
            Source: source,
            Target: target,
            PathNodes: Array.Empty<NodeId>(),
            SegmentCosts: Array.Empty<float>(),
            ComputedPathCost: float.PositiveInfinity,
            CurvatureProxy: 0f,
            IsValid: false);

    private static float LookupEdge(LocalNeighborhoodGraph graph, NodeId a, NodeId b)
    {
        foreach (var (target, weight) in graph.Neighbors(a))
        {
            if (target.Equals(b))
            {
                return weight;
            }
        }

        return 0f;
    }

    /// <summary>
    /// Curvature proxy — coefficient of variation of segment costs, clamped to 0..1.
    /// A straight line through a flat metric has zero variance; curved paths
    /// across heterogeneous regions accumulate variance.
    /// </summary>
    /// <param name="segmentCosts">The path segment costs.</param>
    /// <returns>A 0..1 curvature stand-in.</returns>
    private static float ComputeCurvatureProxy(IReadOnlyList<float> segmentCosts)
    {
        if (segmentCosts.Count < 2)
        {
            return 0f;
        }

        double mean = 0.0;
        foreach (float c in segmentCosts)
        {
            mean += c;
        }

        mean /= segmentCosts.Count;

        if (mean <= 0.0)
        {
            return 0f;
        }

        double variance = 0.0;
        foreach (float c in segmentCosts)
        {
            double diff = c - mean;
            variance += diff * diff;
        }

        variance /= segmentCosts.Count;
        double cv = Math.Sqrt(variance) / mean;
        return (float)Math.Clamp(cv, 0.0, 1.0);
    }
}

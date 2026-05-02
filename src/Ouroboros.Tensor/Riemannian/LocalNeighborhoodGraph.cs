// <copyright file="LocalNeighborhoodGraph.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Riemannian;

/// <summary>
/// A sparse weighted graph used by the <see cref="DiscreteGeodesicReasoner"/>
/// as a discrete approximation of a Riemannian manifold's local structure.
/// </summary>
/// <remarks>
/// <para>
/// Nodes carry float-vector embeddings; edges carry non-negative weights
/// (typically derived from a metric tensor — see <see cref="MetricTensorField"/>).
/// The graph is undirected by convention: callers add edges via
/// <see cref="AddEdge"/> and the implementation stores the symmetric pair.
/// </para>
/// <para>
/// This is intentionally a CPU-side data structure. Tensor backend
/// integration happens at the metric tensor field, not the graph itself.
/// </para>
/// </remarks>
public sealed class LocalNeighborhoodGraph
{
    private readonly Dictionary<NodeId, float[]> _coordinates = new();
    private readonly Dictionary<NodeId, List<(NodeId Target, float Weight)>> _adjacency = new();

    /// <summary>
    /// Gets the count of nodes currently in the graph.
    /// </summary>
    public int NodeCount => _coordinates.Count;

    /// <summary>
    /// Gets the count of unique undirected edges (each pair counted once).
    /// </summary>
    public int EdgeCount
    {
        get
        {
            int total = 0;
            foreach (var entry in _adjacency)
            {
                total += entry.Value.Count;
            }

            return total / 2;
        }
    }

    /// <summary>
    /// Adds a node with optional embedding coordinates. If the node already
    /// exists its coordinates are replaced.
    /// </summary>
    /// <param name="id">The node id.</param>
    /// <param name="coordinates">Optional embedding (a copy is stored).</param>
    public void AddNode(NodeId id, ReadOnlySpan<float> coordinates = default)
    {
        _coordinates[id] = coordinates.IsEmpty ? Array.Empty<float>() : coordinates.ToArray();
        if (!_adjacency.ContainsKey(id))
        {
            _adjacency[id] = new List<(NodeId, float)>();
        }
    }

    /// <summary>
    /// Adds an undirected edge with a non-negative weight. Adds the
    /// endpoints if they are missing.
    /// </summary>
    /// <param name="a">First endpoint.</param>
    /// <param name="b">Second endpoint.</param>
    /// <param name="weight">Edge weight (must be &gt;= 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="weight"/> is negative.</exception>
    public void AddEdge(NodeId a, NodeId b, float weight)
    {
        if (weight < 0f || float.IsNaN(weight))
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Edge weight must be non-negative and finite.");
        }

        if (!_coordinates.ContainsKey(a))
        {
            AddNode(a);
        }

        if (!_coordinates.ContainsKey(b))
        {
            AddNode(b);
        }

        if (a.Equals(b))
        {
            // Self-loops are ignored — they do not contribute to shortest paths.
            return;
        }

        _adjacency[a].Add((b, weight));
        _adjacency[b].Add((a, weight));
    }

    /// <summary>
    /// Returns the neighbors of a node. Empty enumerable if the node does not exist.
    /// </summary>
    /// <param name="id">The node.</param>
    /// <returns>Neighbor (target, weight) pairs.</returns>
    public IReadOnlyList<(NodeId Target, float Weight)> Neighbors(NodeId id)
    {
        return _adjacency.TryGetValue(id, out var list) ? list : Array.Empty<(NodeId, float)>();
    }

    /// <summary>
    /// Returns the embedding coordinates for a node, or empty if the node
    /// does not exist or has no coordinates.
    /// </summary>
    /// <param name="id">The node.</param>
    /// <returns>The coordinate array (do not mutate).</returns>
    public ReadOnlySpan<float> Coordinates(NodeId id)
    {
        return _coordinates.TryGetValue(id, out float[]? coords) ? coords : ReadOnlySpan<float>.Empty;
    }

    /// <summary>
    /// Checks whether the graph contains a node.
    /// </summary>
    /// <param name="id">The node.</param>
    /// <returns>True if present.</returns>
    public bool Contains(NodeId id) => _coordinates.ContainsKey(id);

    /// <summary>
    /// Enumerates all node ids in insertion order is NOT guaranteed.
    /// </summary>
    public IEnumerable<NodeId> Nodes => _coordinates.Keys;
}

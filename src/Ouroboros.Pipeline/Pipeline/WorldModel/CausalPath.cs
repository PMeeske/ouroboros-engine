namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Represents a causal path between two nodes.
/// </summary>
/// <param name="Nodes">The ordered list of nodes in the path.</param>
/// <param name="Edges">The edges connecting the nodes.</param>
/// <param name="TotalStrength">The cumulative strength of the path (product of edge strengths).</param>
public sealed record CausalPath(
    ImmutableList<CausalNode> Nodes,
    ImmutableList<CausalEdge> Edges,
    double TotalStrength)
{
    /// <summary>
    /// Gets the length of the path (number of edges).
    /// </summary>
    public int Length => Edges.Count;

    /// <summary>
    /// Creates an empty path starting from a node.
    /// </summary>
    /// <param name="startNode">The starting node.</param>
    /// <returns>A new causal path with just the start node.</returns>
    public static CausalPath FromNode(CausalNode startNode)
    {
        ArgumentNullException.ThrowIfNull(startNode);

        return new CausalPath(
            ImmutableList.Create(startNode),
            ImmutableList<CausalEdge>.Empty,
            1.0);
    }

    /// <summary>
    /// Extends the path with a new node and edge.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <param name="edge">The edge connecting to the new node.</param>
    /// <returns>A new extended path.</returns>
    public CausalPath Extend(CausalNode node, CausalEdge edge)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(edge);

        return new CausalPath(
            Nodes.Add(node),
            Edges.Add(edge),
            TotalStrength * edge.Strength);
    }
}
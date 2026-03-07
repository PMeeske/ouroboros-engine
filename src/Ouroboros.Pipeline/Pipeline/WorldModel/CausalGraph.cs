// <copyright file="CausalGraph.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.WorldModel;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;

/// <summary>
/// An immutable directed graph modeling cause-effect relationships for predictive planning.
/// Supports causal inference, effect prediction, and path finding between nodes.
/// </summary>
public sealed partial class CausalGraph
{
    private readonly ImmutableDictionary<Guid, CausalNode> _nodes;
    private readonly ImmutableList<CausalEdge> _edges;
    private readonly ImmutableDictionary<Guid, ImmutableList<CausalEdge>> _outgoingEdges;
    private readonly ImmutableDictionary<Guid, ImmutableList<CausalEdge>> _incomingEdges;

    /// <summary>
    /// Initializes a new instance of the <see cref="CausalGraph"/> class.
    /// </summary>
    private CausalGraph(
        ImmutableDictionary<Guid, CausalNode> nodes,
        ImmutableList<CausalEdge> edges,
        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> outgoingEdges,
        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> incomingEdges)
    {
        _nodes = nodes;
        _edges = edges;
        _outgoingEdges = outgoingEdges;
        _incomingEdges = incomingEdges;
    }

    /// <summary>
    /// Gets all nodes in the graph.
    /// </summary>
    public IReadOnlyCollection<CausalNode> Nodes => _nodes.Values.ToImmutableList();

    /// <summary>
    /// Gets all edges in the graph.
    /// </summary>
    public IReadOnlyCollection<CausalEdge> Edges => _edges;

    /// <summary>
    /// Gets the number of nodes in the graph.
    /// </summary>
    public int NodeCount => _nodes.Count;

    /// <summary>
    /// Gets the number of edges in the graph.
    /// </summary>
    public int EdgeCount => _edges.Count;

    /// <summary>
    /// Creates an empty causal graph.
    /// </summary>
    /// <returns>A new empty causal graph.</returns>
    public static CausalGraph Empty()
    {
        return new CausalGraph(
            ImmutableDictionary<Guid, CausalNode>.Empty,
            ImmutableList<CausalEdge>.Empty,
            ImmutableDictionary<Guid, ImmutableList<CausalEdge>>.Empty,
            ImmutableDictionary<Guid, ImmutableList<CausalEdge>>.Empty);
    }

    /// <summary>
    /// Creates a causal graph from collections of nodes and edges.
    /// </summary>
    /// <param name="nodes">The nodes to include.</param>
    /// <param name="edges">The edges to include.</param>
    /// <returns>A Result containing the new graph or an error if validation fails.</returns>
    public static Result<CausalGraph, string> Create(
        IEnumerable<CausalNode> nodes,
        IEnumerable<CausalEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        CausalGraph graph = Empty();

        foreach (CausalNode node in nodes)
        {
            Result<CausalGraph, string> nodeResult = graph.AddNode(node);
            if (nodeResult.IsFailure)
            {
                return nodeResult;
            }

            graph = nodeResult.Value;
        }

        foreach (CausalEdge edge in edges)
        {
            Result<CausalGraph, string> edgeResult = graph.AddEdge(edge);
            if (edgeResult.IsFailure)
            {
                return edgeResult;
            }

            graph = edgeResult.Value;
        }

        return Result<CausalGraph, string>.Success(graph);
    }

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <returns>A Result containing a new graph with the node added, or an error.</returns>
    public Result<CausalGraph, string> AddNode(CausalNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (_nodes.ContainsKey(node.Id))
        {
            return Result<CausalGraph, string>.Failure($"Node with ID {node.Id} already exists in the graph.");
        }

        ImmutableDictionary<Guid, CausalNode> newNodes = _nodes.Add(node.Id, node);

        return Result<CausalGraph, string>.Success(
            new CausalGraph(newNodes, _edges, _outgoingEdges, _incomingEdges));
    }

    /// <summary>
    /// Adds an edge to the graph.
    /// </summary>
    /// <param name="edge">The edge to add.</param>
    /// <returns>A Result containing a new graph with the edge added, or an error.</returns>
    public Result<CausalGraph, string> AddEdge(CausalEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        if (!_nodes.ContainsKey(edge.SourceId))
        {
            return Result<CausalGraph, string>.Failure($"Source node with ID {edge.SourceId} does not exist in the graph.");
        }

        if (!_nodes.ContainsKey(edge.TargetId))
        {
            return Result<CausalGraph, string>.Failure($"Target node with ID {edge.TargetId} does not exist in the graph.");
        }

        ImmutableList<CausalEdge> newEdges = _edges.Add(edge);

        ImmutableList<CausalEdge> existingOutgoing = _outgoingEdges.GetValueOrDefault(
            edge.SourceId,
            ImmutableList<CausalEdge>.Empty);
        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> newOutgoing =
            _outgoingEdges.SetItem(edge.SourceId, existingOutgoing.Add(edge));

        ImmutableList<CausalEdge> existingIncoming = _incomingEdges.GetValueOrDefault(
            edge.TargetId,
            ImmutableList<CausalEdge>.Empty);
        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> newIncoming =
            _incomingEdges.SetItem(edge.TargetId, existingIncoming.Add(edge));

        return Result<CausalGraph, string>.Success(
            new CausalGraph(_nodes, newEdges, newOutgoing, newIncoming));
    }

    /// <summary>
    /// Removes a node and all its connected edges from the graph.
    /// </summary>
    /// <param name="nodeId">The ID of the node to remove.</param>
    /// <returns>A Result containing a new graph with the node removed, or an error.</returns>
    public Result<CausalGraph, string> RemoveNode(Guid nodeId)
    {
        if (!_nodes.ContainsKey(nodeId))
        {
            return Result<CausalGraph, string>.Failure($"Node with ID {nodeId} does not exist in the graph.");
        }

        ImmutableDictionary<Guid, CausalNode> newNodes = _nodes.Remove(nodeId);
        ImmutableList<CausalEdge> newEdges = _edges
            .Where(e => e.SourceId != nodeId && e.TargetId != nodeId)
            .ToImmutableList();

        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> newOutgoing = _outgoingEdges.Remove(nodeId);
        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> newIncoming = _incomingEdges.Remove(nodeId);

        // Remove references to this node from other nodes' edge lists
        foreach (Guid key in newOutgoing.Keys.ToList())
        {
            ImmutableList<CausalEdge> filtered = newOutgoing[key]
                .Where(e => e.TargetId != nodeId)
                .ToImmutableList();
            newOutgoing = newOutgoing.SetItem(key, filtered);
        }

        foreach (Guid key in newIncoming.Keys.ToList())
        {
            ImmutableList<CausalEdge> filtered = newIncoming[key]
                .Where(e => e.SourceId != nodeId)
                .ToImmutableList();
            newIncoming = newIncoming.SetItem(key, filtered);
        }

        return Result<CausalGraph, string>.Success(
            new CausalGraph(newNodes, newEdges, newOutgoing, newIncoming));
    }

    /// <summary>
    /// Gets a node by its ID.
    /// </summary>
    /// <param name="id">The node ID.</param>
    /// <returns>An Option containing the node if found.</returns>
    public Option<CausalNode> GetNode(Guid id)
    {
        return _nodes.TryGetValue(id, out CausalNode? node)
            ? Option<CausalNode>.Some(node)
            : Option<CausalNode>.None();
    }

    /// <summary>
    /// Gets a node by its name.
    /// </summary>
    /// <param name="name">The node name.</param>
    /// <returns>An Option containing the first node with the matching name if found.</returns>
    public Option<CausalNode> GetNodeByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        CausalNode? node = _nodes.Values
            .FirstOrDefault(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return node is not null
            ? Option<CausalNode>.Some(node)
            : Option<CausalNode>.None();
    }

    /// <summary>
    /// Gets all nodes that cause the specified node (direct predecessors).
    /// </summary>
    /// <param name="nodeId">The ID of the target node.</param>
    /// <returns>A list of nodes that have causal edges pointing to the specified node.</returns>
    public IReadOnlyList<CausalNode> GetCauses(Guid nodeId)
    {
        if (!_incomingEdges.TryGetValue(nodeId, out ImmutableList<CausalEdge>? incoming))
        {
            return ImmutableList<CausalNode>.Empty;
        }

        return incoming
            .Select(e => _nodes.GetValueOrDefault(e.SourceId))
            .Where(n => n is not null)
            .Cast<CausalNode>()
            .ToImmutableList();
    }

    /// <summary>
    /// Gets all nodes that are effects of the specified node (direct successors).
    /// </summary>
    /// <param name="nodeId">The ID of the source node.</param>
    /// <returns>A list of nodes that the specified node has causal edges pointing to.</returns>
    public IReadOnlyList<CausalNode> GetEffects(Guid nodeId)
    {
        if (!_outgoingEdges.TryGetValue(nodeId, out ImmutableList<CausalEdge>? outgoing))
        {
            return ImmutableList<CausalNode>.Empty;
        }

        return outgoing
            .Select(e => _nodes.GetValueOrDefault(e.TargetId))
            .Where(n => n is not null)
            .Cast<CausalNode>()
            .ToImmutableList();
    }

    /// <summary>
    /// Gets the causal edges between two nodes.
    /// </summary>
    /// <param name="sourceId">The source node ID.</param>
    /// <param name="targetId">The target node ID.</param>
    /// <returns>A list of edges connecting the source to the target.</returns>
    public IReadOnlyList<CausalEdge> GetEdgesBetween(Guid sourceId, Guid targetId)
    {
        if (!_outgoingEdges.TryGetValue(sourceId, out ImmutableList<CausalEdge>? outgoing))
        {
            return ImmutableList<CausalEdge>.Empty;
        }

        return outgoing
            .Where(e => e.TargetId == targetId)
            .ToImmutableList();
    }

    /// <summary>
    /// Determines whether the graph contains a cycle using depth-first search.
    /// </summary>
    /// <returns>True if a cycle exists; otherwise false.</returns>
    public bool HasCycle()
    {
        var visited = new HashSet<Guid>();
        var inStack = new HashSet<Guid>();

        foreach (Guid nodeId in _nodes.Keys)
        {
            if (!visited.Contains(nodeId) && HasCycleDfs(nodeId, visited, inStack))
                return true;
        }

        return false;
    }

    private bool HasCycleDfs(Guid nodeId, HashSet<Guid> visited, HashSet<Guid> inStack)
    {
        visited.Add(nodeId);
        inStack.Add(nodeId);

        if (_outgoingEdges.TryGetValue(nodeId, out ImmutableList<CausalEdge>? outgoing))
        {
            foreach (CausalEdge edge in outgoing)
            {
                if (inStack.Contains(edge.TargetId))
                    return true;
                if (!visited.Contains(edge.TargetId) && HasCycleDfs(edge.TargetId, visited, inStack))
                    return true;
            }
        }

        inStack.Remove(nodeId);
        return false;
    }

    /// <summary>
    /// Gets all nodes that have no incoming edges (root nodes).
    /// </summary>
    /// <returns>A list of nodes with no incoming edges.</returns>
    public IReadOnlyList<CausalNode> GetRootNodes()
    {
        return _nodes.Values
            .Where(n => !_incomingEdges.TryGetValue(n.Id, out ImmutableList<CausalEdge>? incoming) || incoming.Count == 0)
            .ToImmutableList();
    }

    /// <summary>
    /// Gets all nodes that have no outgoing edges (leaf nodes).
    /// </summary>
    /// <returns>A list of nodes with no outgoing edges.</returns>
    public IReadOnlyList<CausalNode> GetLeafNodes()
    {
        return _nodes.Values
            .Where(n => !_outgoingEdges.TryGetValue(n.Id, out ImmutableList<CausalEdge>? outgoing) || outgoing.Count == 0)
            .ToImmutableList();
    }

    /// <summary>
    /// Gets all nodes of the specified type.
    /// </summary>
    /// <param name="type">The node type to filter by.</param>
    /// <returns>A list of nodes matching the specified type.</returns>
    public IReadOnlyList<CausalNode> GetNodesByType(CausalNodeType type)
    {
        return _nodes.Values
            .Where(n => n.NodeType == type)
            .ToImmutableList();
    }

}

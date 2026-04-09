// <copyright file="MerkleDag.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Network;

/// <summary>
/// Represents a Merkle-DAG (Directed Acyclic Graph) of monadic nodes and transitions.
/// Provides efficient storage, retrieval, and traversal of the network state.
/// </summary>
public sealed class MerkleDag
{
    private readonly Dictionary<Guid, MonadNode> _nodes;
    private readonly Dictionary<Guid, TransitionEdge> _edges;
    private readonly Dictionary<Guid, List<Guid>> _nodeToIncomingEdges;
    private readonly Dictionary<Guid, List<Guid>> _nodeToOutgoingEdges;
    private readonly object _syncLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleDag"/> class.
    /// </summary>
    public MerkleDag()
    {
        _nodes = new Dictionary<Guid, MonadNode>();
        _edges = new Dictionary<Guid, TransitionEdge>();
        _nodeToIncomingEdges = new Dictionary<Guid, List<Guid>>();
        _nodeToOutgoingEdges = new Dictionary<Guid, List<Guid>>();
    }

    /// <summary>
    /// Gets all nodes in the DAG.
    /// </summary>
    public IReadOnlyDictionary<Guid, MonadNode> Nodes => _nodes;

    /// <summary>
    /// Gets all edges in the DAG.
    /// </summary>
    public IReadOnlyDictionary<Guid, TransitionEdge> Edges => _edges;

    /// <summary>
    /// Gets the count of nodes in the DAG.
    /// </summary>
    public int NodeCount => _nodes.Count;

    /// <summary>
    /// Gets the count of edges in the DAG.
    /// </summary>
    public int EdgeCount => _edges.Count;

    /// <summary>
    /// Adds a node to the DAG.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result<MonadNode> AddNode(MonadNode node)
    {
        if (node == null)
        {
            return Result<MonadNode>.Failure("Node cannot be null");
        }

        if (!node.VerifyHash())
        {
            return Result<MonadNode>.Failure("Node hash verification failed");
        }

        lock (_syncLock)
        {
            if (_nodes.ContainsKey(node.Id))
            {
                return Result<MonadNode>.Failure($"Node with ID {node.Id} already exists");
            }

            // Verify parent nodes exist
            var missingParent = node.ParentIds.Cast<Guid?>().FirstOrDefault(id => !_nodes.ContainsKey(id!.Value));
            if (missingParent.HasValue)
            {
                return Result<MonadNode>.Failure($"Parent node {missingParent.Value} does not exist");
            }

            _nodes[node.Id] = node;
            _nodeToIncomingEdges[node.Id] = new List<Guid>();
            _nodeToOutgoingEdges[node.Id] = new List<Guid>();

            return Result<MonadNode>.Success(node);
        }
    }

    /// <summary>
    /// Adds an edge to the DAG.
    /// </summary>
    /// <param name="edge">The edge to add.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result<TransitionEdge> AddEdge(TransitionEdge edge)
    {
        if (edge == null)
        {
            return Result<TransitionEdge>.Failure("Edge cannot be null");
        }

        if (!edge.VerifyHash())
        {
            return Result<TransitionEdge>.Failure("Edge hash verification failed");
        }

        lock (_syncLock)
        {
            if (_edges.ContainsKey(edge.Id))
            {
                return Result<TransitionEdge>.Failure($"Edge with ID {edge.Id} already exists");
            }

            // Verify all input and output nodes exist
            var missingInput = edge.InputIds.Cast<Guid?>().FirstOrDefault(id => !_nodes.ContainsKey(id!.Value));
            if (missingInput.HasValue)
            {
                return Result<TransitionEdge>.Failure($"Input node {missingInput.Value} does not exist");
            }

            if (!_nodes.ContainsKey(edge.OutputId))
            {
                return Result<TransitionEdge>.Failure($"Output node {edge.OutputId} does not exist");
            }

            _edges[edge.Id] = edge;

            // Update adjacency information
            foreach (var inputId in edge.InputIds)
            {
                _nodeToOutgoingEdges[inputId].Add(edge.Id);
            }

            _nodeToIncomingEdges[edge.OutputId].Add(edge.Id);

            return Result<TransitionEdge>.Success(edge);
        }
    }

    /// <summary>
    /// Gets a node by its ID.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>An Option containing the node if found.</returns>
    public Option<MonadNode> GetNode(Guid nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node)
            ? Option<MonadNode>.Some(node)
            : Option<MonadNode>.None();
    }

    /// <summary>
    /// Gets an edge by its ID.
    /// </summary>
    /// <param name="edgeId">The edge ID.</param>
    /// <returns>An Option containing the edge if found.</returns>
    public Option<TransitionEdge> GetEdge(Guid edgeId)
    {
        return _edges.TryGetValue(edgeId, out var edge)
            ? Option<TransitionEdge>.Some(edge)
            : Option<TransitionEdge>.None();
    }

    /// <summary>
    /// Gets all incoming edges for a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>A collection of incoming edges.</returns>
    public IEnumerable<TransitionEdge> GetIncomingEdges(Guid nodeId)
    {
        if (!_nodeToIncomingEdges.TryGetValue(nodeId, out var edgeIds))
        {
            return Enumerable.Empty<TransitionEdge>();
        }

        return edgeIds.Select(id => _edges[id]);
    }

    /// <summary>
    /// Gets all outgoing edges for a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>A collection of outgoing edges.</returns>
    public IEnumerable<TransitionEdge> GetOutgoingEdges(Guid nodeId)
    {
        if (!_nodeToOutgoingEdges.TryGetValue(nodeId, out var edgeIds))
        {
            return Enumerable.Empty<TransitionEdge>();
        }

        return edgeIds.Select(id => _edges[id]);
    }

    /// <summary>
    /// Gets all root nodes (nodes with no parents).
    /// </summary>
    /// <returns>A collection of root nodes.</returns>
    public IEnumerable<MonadNode> GetRootNodes()
    {
        return _nodes.Values.Where(n => n.ParentIds.IsEmpty);
    }

    /// <summary>
    /// Gets all leaf nodes (nodes with no outgoing edges).
    /// </summary>
    /// <returns>A collection of leaf nodes.</returns>
    public IEnumerable<MonadNode> GetLeafNodes()
    {
        return _nodes.Values.Where(n =>
            !_nodeToOutgoingEdges.TryGetValue(n.Id, out var edges) || edges.Count == 0);
    }

    /// <summary>
    /// Performs a topological sort of the DAG.
    /// </summary>
    /// <returns>A Result containing the sorted nodes or an error if the graph has cycles.</returns>
    public Result<ImmutableArray<MonadNode>> TopologicalSort()
    {
        var inDegree = _nodes.Values.ToDictionary(
            node => node.Id,
            node => GetIncomingEdges(node.Id).Count());
        var queue = new Queue<Guid>();
        var sorted = new List<MonadNode>();

        foreach (var id in inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key))
        {
            queue.Enqueue(id);
        }

        // Process nodes
        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            sorted.Add(_nodes[nodeId]);

            // Reduce in-degree for children
            foreach (var edge in GetOutgoingEdges(nodeId))
            {
                inDegree[edge.OutputId]--;
            }

            foreach (var childId in GetOutgoingEdges(nodeId)
                         .Select(e => e.OutputId)
                         .Where(id => inDegree[id] == 0))
            {
                queue.Enqueue(childId);
            }
        }

        if (sorted.Count != _nodes.Count)
        {
            return Result<ImmutableArray<MonadNode>>.Failure("Graph contains cycles");
        }

        return Result<ImmutableArray<MonadNode>>.Success(sorted.ToImmutableArray());
    }

    /// <summary>
    /// Gets all nodes of a specific type.
    /// </summary>
    /// <param name="typeName">The type name to filter by.</param>
    /// <returns>A collection of nodes with the specified type.</returns>
    public IEnumerable<MonadNode> GetNodesByType(string typeName)
    {
        return _nodes.Values.Where(n => n.TypeName == typeName);
    }

    /// <summary>
    /// Gets all transitions with a specific operation name.
    /// </summary>
    /// <param name="operationName">The operation name to filter by.</param>
    /// <returns>A collection of edges with the specified operation.</returns>
    public IEnumerable<TransitionEdge> GetTransitionsByOperation(string operationName)
    {
        return _edges.Values.Where(e => e.OperationName == operationName);
    }

    /// <summary>
    /// Removes an edge from the DAG.
    /// </summary>
    /// <param name="edgeId">The ID of the edge to remove.</param>
    /// <returns>A Result indicating whether the removal succeeded.</returns>
    public Result<bool> RemoveEdge(Guid edgeId)
    {
        lock (_syncLock)
        {
            if (!_edges.TryGetValue(edgeId, out var edge))
            {
                return Result<bool>.Failure($"Edge {edgeId} not found");
            }

            // Remove from edges collection
            _edges.Remove(edgeId);

            // Remove from outgoing adjacency lists
            foreach (var inputId in edge.InputIds)
            {
                if (_nodeToOutgoingEdges.TryGetValue(inputId, out var outgoing))
                {
                    outgoing.Remove(edgeId);
                }
            }

            // Remove from incoming adjacency list
            if (_nodeToIncomingEdges.TryGetValue(edge.OutputId, out var incoming))
            {
                incoming.Remove(edgeId);
            }

            return Result<bool>.Success(true);
        }
    }

    /// <summary>
    /// Replaces an edge with a new version that has the same ID but updated properties.
    /// Used for modifying edge weights (confidence) in-place.
    /// </summary>
    /// <param name="edge">The replacement edge (must have the same ID as an existing edge).</param>
    /// <returns>A Result indicating whether the update succeeded.</returns>
    public Result<TransitionEdge> UpdateEdge(TransitionEdge edge)
    {
        if (edge == null)
        {
            return Result<TransitionEdge>.Failure("Edge cannot be null");
        }

        lock (_syncLock)
        {
            if (!_edges.ContainsKey(edge.Id))
            {
                return Result<TransitionEdge>.Failure($"Edge {edge.Id} not found");
            }

            // Remove old edge from adjacency lists
            var oldEdge = _edges[edge.Id];
            foreach (var inputId in oldEdge.InputIds)
            {
                if (_nodeToOutgoingEdges.TryGetValue(inputId, out var outgoing))
                {
                    outgoing.Remove(edge.Id);
                }
            }

            if (_nodeToIncomingEdges.TryGetValue(oldEdge.OutputId, out var incoming))
            {
                incoming.Remove(edge.Id);
            }

            // Replace the edge
            _edges[edge.Id] = edge;

            // Add new edge to adjacency lists
            foreach (var inputId in edge.InputIds)
            {
                if (_nodeToOutgoingEdges.TryGetValue(inputId, out var outgoing))
                {
                    outgoing.Add(edge.Id);
                }
            }

            if (_nodeToIncomingEdges.TryGetValue(edge.OutputId, out var incomingList))
            {
                incomingList.Add(edge.Id);
            }

            return Result<TransitionEdge>.Success(edge);
        }
    }

    /// <summary>
    /// Removes a node and all its connected edges from the DAG.
    /// </summary>
    /// <param name="nodeId">The ID of the node to remove.</param>
    /// <returns>A Result indicating whether the removal succeeded.</returns>
    public Result<bool> RemoveNode(Guid nodeId)
    {
        lock (_syncLock)
        {
            if (!_nodes.ContainsKey(nodeId))
            {
                return Result<bool>.Failure($"Node {nodeId} not found");
            }

            // Remove all outgoing edges
            if (_nodeToOutgoingEdges.TryGetValue(nodeId, out var outgoingEdgeIds))
            {
                foreach (var edgeId in outgoingEdgeIds.ToList())
                {
                    if (_edges.TryGetValue(edgeId, out var edge))
                    {
                        // Remove from incoming list of the output node
                        if (_nodeToIncomingEdges.TryGetValue(edge.OutputId, out var incomingList))
                        {
                            incomingList.Remove(edgeId);
                        }

                        _edges.Remove(edgeId);
                    }
                }
            }

            // Remove all incoming edges
            if (_nodeToIncomingEdges.TryGetValue(nodeId, out var incomingEdgeIds))
            {
                foreach (var edgeId in incomingEdgeIds.ToList())
                {
                    if (_edges.TryGetValue(edgeId, out var edge))
                    {
                        // Remove from outgoing lists of input nodes
                        foreach (var inputId in edge.InputIds)
                        {
                            if (_nodeToOutgoingEdges.TryGetValue(inputId, out var outgoingList))
                            {
                                outgoingList.Remove(edgeId);
                            }
                        }

                        _edges.Remove(edgeId);
                    }
                }
            }

            // Remove the node and its adjacency entries
            _nodes.Remove(nodeId);
            _nodeToOutgoingEdges.Remove(nodeId);
            _nodeToIncomingEdges.Remove(nodeId);

            return Result<bool>.Success(true);
        }
    }

    /// <summary>
    /// Updates a node by replacing it with a new version (same ID, different payload).
    /// The node's hash is recomputed automatically.
    /// </summary>
    /// <param name="node">The replacement node (must have the same ID as an existing node).</param>
    /// <returns>A Result containing the updated node or an error.</returns>
    public Result<MonadNode> UpdateNode(MonadNode node)
    {
        if (node == null)
        {
            return Result<MonadNode>.Failure("Node cannot be null");
        }

        if (!node.VerifyHash())
        {
            return Result<MonadNode>.Failure("Node hash verification failed");
        }

        lock (_syncLock)
        {
            if (!_nodes.ContainsKey(node.Id))
            {
                return Result<MonadNode>.Failure($"Node {node.Id} not found");
            }

            _nodes[node.Id] = node;
            return Result<MonadNode>.Success(node);
        }
    }

    /// <summary>
    /// Gets all edges originating from a node, keyed by edge ID.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>A dictionary of edges keyed by edge ID.</returns>
    public IReadOnlyDictionary<Guid, TransitionEdge> GetEdgesFrom(Guid nodeId)
    {
        lock (_syncLock)
        {
            if (!_nodeToOutgoingEdges.TryGetValue(nodeId, out var edgeIds))
            {
                return new Dictionary<Guid, TransitionEdge>();
            }

            return edgeIds
                .Where(id => _edges.ContainsKey(id))
                .ToDictionary(id => id, id => _edges[id]);
        }
    }

    /// <summary>
    /// Verifies the integrity of the entire DAG.
    /// </summary>
    /// <returns>A Result indicating whether the DAG is valid.</returns>
    public Result<bool> VerifyIntegrity()
    {
        // Verify all node hashes
        var invalidNode = _nodes.Values.FirstOrDefault(n => !n.VerifyHash());
        if (invalidNode != null)
        {
            return Result<bool>.Failure($"Node {invalidNode.Id} hash verification failed");
        }

        // Verify all edge hashes
        var invalidEdge = _edges.Values.FirstOrDefault(e => !e.VerifyHash());
        if (invalidEdge != null)
        {
            return Result<bool>.Failure($"Edge {invalidEdge.Id} hash verification failed");
        }

        // Verify no cycles
        var sortResult = TopologicalSort();
        if (sortResult.IsFailure)
        {
            return Result<bool>.Failure(sortResult.Error);
        }

        return Result<bool>.Success(true);
    }
}

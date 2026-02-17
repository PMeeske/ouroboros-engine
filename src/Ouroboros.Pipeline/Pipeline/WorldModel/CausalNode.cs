namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Represents a node in the causal graph.
/// </summary>
/// <param name="Id">Unique identifier for the node.</param>
/// <param name="Name">Human-readable name of the node.</param>
/// <param name="Description">Detailed description of what this node represents.</param>
/// <param name="NodeType">The type of causal node (State, Action, or Event).</param>
public sealed record CausalNode(
    Guid Id,
    string Name,
    string Description,
    CausalNodeType NodeType)
{
    /// <summary>
    /// Creates a new causal node with a generated ID.
    /// </summary>
    /// <param name="name">The name of the node.</param>
    /// <param name="description">The description of the node.</param>
    /// <param name="nodeType">The type of node.</param>
    /// <returns>A new causal node.</returns>
    public static CausalNode Create(string name, string description, CausalNodeType nodeType)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        return new CausalNode(Guid.NewGuid(), name, description, nodeType);
    }

    /// <summary>
    /// Creates a new state node.
    /// </summary>
    /// <param name="name">The name of the state.</param>
    /// <param name="description">The description of the state.</param>
    /// <returns>A new state node.</returns>
    public static CausalNode CreateState(string name, string description)
    {
        return Create(name, description, CausalNodeType.State);
    }

    /// <summary>
    /// Creates a new action node.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="description">The description of the action.</param>
    /// <returns>A new action node.</returns>
    public static CausalNode CreateAction(string name, string description)
    {
        return Create(name, description, CausalNodeType.Action);
    }

    /// <summary>
    /// Creates a new event node.
    /// </summary>
    /// <param name="name">The name of the event.</param>
    /// <param name="description">The description of the event.</param>
    /// <returns>A new event node.</returns>
    public static CausalNode CreateEvent(string name, string description)
    {
        return Create(name, description, CausalNodeType.Event);
    }
}
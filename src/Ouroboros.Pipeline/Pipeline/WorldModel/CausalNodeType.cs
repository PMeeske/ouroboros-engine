namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Defines the type of causal node in the graph.
/// </summary>
public enum CausalNodeType
{
    /// <summary>
    /// Represents a state or condition in the world.
    /// </summary>
    State,

    /// <summary>
    /// Represents an action that can be taken.
    /// </summary>
    Action,

    /// <summary>
    /// Represents an event that occurs.
    /// </summary>
    Event,
}
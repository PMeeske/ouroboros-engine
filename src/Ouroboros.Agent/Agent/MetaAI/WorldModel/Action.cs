namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Represents an action that can be taken in the environment.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Name">The name/type of the action.</param>
/// <param name="Parameters">Dictionary of parameters for the action.</param>
public sealed record Action(
    string Name,
    Dictionary<string, object> Parameters);
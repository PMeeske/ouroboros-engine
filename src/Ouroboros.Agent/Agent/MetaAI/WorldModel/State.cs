namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Represents a state in the environment with features and embedding.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Features">Dictionary of named features for the state.</param>
/// <param name="Embedding">Vector embedding representation of the state.</param>
public sealed record State(
    Dictionary<string, object> Features,
    float[] Embedding);
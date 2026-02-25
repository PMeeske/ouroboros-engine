namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Represents a logical conflict between two rules.
/// Rules may be null when conflicts are detected through analysis but specific rules cannot be identified.
/// </summary>
public sealed record LogicalConflict(
    string Description,
    SymbolicRule? Rule1,
    SymbolicRule? Rule2,
    string Resolution);
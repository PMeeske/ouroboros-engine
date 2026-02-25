namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a capability that an Ouroboros instance possesses.
/// </summary>
/// <param name="Name">The name of the capability.</param>
/// <param name="Description">Description of what the capability enables.</param>
/// <param name="ConfidenceLevel">How confident the system is in this capability.</param>
public sealed record OuroborosCapability(
    string Name,
    string Description,
    double ConfidenceLevel);
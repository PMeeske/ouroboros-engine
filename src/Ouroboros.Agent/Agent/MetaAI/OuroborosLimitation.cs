namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a limitation or weakness of an Ouroboros instance.
/// </summary>
/// <param name="Name">The name of the limitation.</param>
/// <param name="Description">Description of the limitation.</param>
/// <param name="Mitigation">Possible mitigation strategies.</param>
public sealed record OuroborosLimitation(
    string Name,
    string Description,
    string? Mitigation = null);
namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Represents a minority opinion that was not adopted but is recorded for transparency.
/// </summary>
/// <param name="AgentName">Name of the dissenting agent.</param>
/// <param name="Position">The dissenting position.</param>
/// <param name="Rationale">Explanation for the dissent.</param>
/// <param name="Concerns">Specific concerns that remain unaddressed.</param>
public sealed record MinorityOpinion(
    string AgentName,
    string Position,
    string Rationale,
    IReadOnlyList<string> Concerns);
namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Represents a single round in the council debate.
/// </summary>
/// <param name="Phase">The debate phase (Proposal, Challenge, Refinement, Voting, Synthesis).</param>
/// <param name="RoundNumber">The round number within the phase.</param>
/// <param name="Contributions">List of agent contributions in this round.</param>
/// <param name="Timestamp">When this round occurred.</param>
public sealed record DebateRound(
    DebatePhase Phase,
    int RoundNumber,
    IReadOnlyList<AgentContribution> Contributions,
    DateTime Timestamp);
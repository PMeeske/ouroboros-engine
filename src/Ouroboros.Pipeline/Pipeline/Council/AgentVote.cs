namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Represents a single agent's vote with position, weight, and rationale.
/// </summary>
/// <param name="AgentName">Name of the voting agent.</param>
/// <param name="Position">The agent's position (Approve, Reject, Abstain, etc.).</param>
/// <param name="Weight">The weight of this vote based on expertise (0.0 to 1.0).</param>
/// <param name="Rationale">Explanation for the vote.</param>
public sealed record AgentVote(
    string AgentName,
    string Position,
    double Weight,
    string Rationale);
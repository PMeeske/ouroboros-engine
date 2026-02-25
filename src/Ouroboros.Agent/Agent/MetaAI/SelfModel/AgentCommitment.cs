namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Represents a commitment made by the agent.
/// </summary>
public sealed record AgentCommitment(
    Guid Id,
    string Description,
    DateTime Deadline,
    double Priority,
    CommitmentStatus Status,
    double ProgressPercent,
    List<string> Dependencies,
    Dictionary<string, object> Metadata,
    DateTime CreatedAt,
    DateTime? CompletedAt);
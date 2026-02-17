namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a distributed task assignment.
/// </summary>
public sealed record TaskAssignment(
    string TaskId,
    string AgentId,
    PlanStep Step,
    DateTime AssignedAt,
    TaskAssignmentStatus Status);
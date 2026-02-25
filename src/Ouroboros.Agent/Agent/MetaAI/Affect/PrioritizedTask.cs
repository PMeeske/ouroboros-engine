namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// Represents a task with priority modulation.
/// </summary>
public sealed record PrioritizedTask(
    Guid Id,
    string Name,
    string Description,
    double BasePriority,
    double ModulatedPriority,
    TaskAppraisal Appraisal,
    DateTime CreatedAt,
    DateTime? DueAt,
    TaskStatus Status,
    Dictionary<string, object> Metadata);
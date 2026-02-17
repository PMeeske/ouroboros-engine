namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a plan with temporal constraints and scheduling.
/// </summary>
public sealed record TemporalPlan(
    string Goal,
    List<ScheduledTask> Tasks,
    TimeSpan TotalDuration);
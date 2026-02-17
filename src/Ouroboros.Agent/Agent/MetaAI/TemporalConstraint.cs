namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a temporal constraint between two tasks.
/// </summary>
public sealed record TemporalConstraint(
    string TaskA,
    string TaskB,
    TemporalRelation Relation,
    TimeSpan? Duration = null);
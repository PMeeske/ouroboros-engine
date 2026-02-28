namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents an adaptation trigger condition.
/// </summary>
public sealed record AdaptationTrigger(
    string Name,
    Func<PlanExecutionContext, bool> Condition,
    AdaptationStrategy Strategy);
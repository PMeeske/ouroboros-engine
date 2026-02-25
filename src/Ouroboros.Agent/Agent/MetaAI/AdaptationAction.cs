namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents an adaptation action.
/// </summary>
public sealed record AdaptationAction(
    AdaptationStrategy Strategy,
    string Reason,
    Plan? RevisedPlan = null,
    PlanStep? ReplacementStep = null);
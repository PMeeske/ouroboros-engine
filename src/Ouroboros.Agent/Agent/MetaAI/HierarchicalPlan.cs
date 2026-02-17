namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a hierarchical plan with multiple levels.
/// </summary>
public sealed record HierarchicalPlan(
    string Goal,
    Plan TopLevelPlan,
    Dictionary<string, Plan> SubPlans,
    int MaxDepth,
    DateTime CreatedAt);
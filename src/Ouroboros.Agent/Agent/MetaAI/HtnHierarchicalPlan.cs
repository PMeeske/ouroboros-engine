namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents an HTN hierarchical plan with abstract and concrete decompositions.
/// </summary>
public sealed record HtnHierarchicalPlan(
    string Goal,
    List<AbstractTask> AbstractTasks,
    List<ConcretePlan> Refinements);
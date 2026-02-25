namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents configuration for hierarchical planning.
/// </summary>
public sealed record HierarchicalPlanningConfig(
    int MaxDepth = 3,
    int MinStepsForDecomposition = 3,
    double ComplexityThreshold = 0.7);
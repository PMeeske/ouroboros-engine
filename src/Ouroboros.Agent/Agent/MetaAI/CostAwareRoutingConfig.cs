namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for cost-aware routing.
/// </summary>
public sealed record CostAwareRoutingConfig(
    double MaxCostPerPlan = 1.0,
    double MinAcceptableQuality = 0.7,
    CostOptimizationStrategy Strategy = CostOptimizationStrategy.Balanced);
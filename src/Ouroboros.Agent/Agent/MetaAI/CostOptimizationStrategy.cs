namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Cost optimization strategy.
/// </summary>
public enum CostOptimizationStrategy
{
    MinimizeCost,
    MaximizeQuality,
    Balanced,
    MaximizeValue // Best quality per cost
}
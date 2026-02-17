namespace Ouroboros.Providers;

/// <summary>
/// Types of optimization suggestions.
/// </summary>
public enum OptimizationType
{
    IncreasePriority,
    ReduceUsage,
    ConsiderRemoving,
    AdjustParameters,
    AddFallback
}
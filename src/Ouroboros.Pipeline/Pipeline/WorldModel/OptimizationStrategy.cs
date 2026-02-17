namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Defines the optimization strategy for tool selection.
/// </summary>
public enum OptimizationStrategy
{
    /// <summary>
    /// Optimize for lowest cost (prefer cheaper tools).
    /// </summary>
    Cost,

    /// <summary>
    /// Optimize for fastest execution speed.
    /// </summary>
    Speed,

    /// <summary>
    /// Optimize for highest quality output.
    /// </summary>
    Quality,

    /// <summary>
    /// Balanced optimization considering all factors.
    /// </summary>
    Balanced,
}
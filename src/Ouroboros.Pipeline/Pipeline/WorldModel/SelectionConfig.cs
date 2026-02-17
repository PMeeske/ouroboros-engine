namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Configuration options for smart tool selection.
/// </summary>
/// <param name="MaxTools">Maximum number of tools to select (default: 5).</param>
/// <param name="MinConfidence">Minimum confidence threshold for selection (0.0 to 1.0, default: 0.3).</param>
/// <param name="OptimizeFor">The optimization strategy to use (default: Balanced).</param>
/// <param name="AllowParallelExecution">Whether selected tools can be executed in parallel (default: true).</param>
public sealed record SelectionConfig(
    int MaxTools = 5,
    double MinConfidence = 0.3,
    OptimizationStrategy OptimizeFor = OptimizationStrategy.Balanced,
    bool AllowParallelExecution = true)
{
    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static SelectionConfig Default { get; } = new();

    /// <summary>
    /// Creates a configuration optimized for cost.
    /// </summary>
    /// <returns>A cost-optimized configuration.</returns>
    public static SelectionConfig ForCost() => new(
        MaxTools: 3,
        MinConfidence: 0.4,
        OptimizeFor: OptimizationStrategy.Cost,
        AllowParallelExecution: false);

    /// <summary>
    /// Creates a configuration optimized for speed.
    /// </summary>
    /// <returns>A speed-optimized configuration.</returns>
    public static SelectionConfig ForSpeed() => new(
        MaxTools: 2,
        MinConfidence: 0.5,
        OptimizeFor: OptimizationStrategy.Speed,
        AllowParallelExecution: true);

    /// <summary>
    /// Creates a configuration optimized for quality.
    /// </summary>
    /// <returns>A quality-optimized configuration.</returns>
    public static SelectionConfig ForQuality() => new(
        MaxTools: 10,
        MinConfidence: 0.2,
        OptimizeFor: OptimizationStrategy.Quality,
        AllowParallelExecution: true);

    /// <summary>
    /// Creates a new configuration with the specified max tools.
    /// </summary>
    /// <param name="maxTools">The maximum number of tools.</param>
    /// <returns>A new configuration with updated max tools.</returns>
    public SelectionConfig WithMaxTools(int maxTools) =>
        this with { MaxTools = Math.Max(1, maxTools) };

    /// <summary>
    /// Creates a new configuration with the specified minimum confidence.
    /// </summary>
    /// <param name="minConfidence">The minimum confidence threshold.</param>
    /// <returns>A new configuration with updated minimum confidence.</returns>
    public SelectionConfig WithMinConfidence(double minConfidence) =>
        this with { MinConfidence = Math.Clamp(minConfidence, 0.0, 1.0) };
}
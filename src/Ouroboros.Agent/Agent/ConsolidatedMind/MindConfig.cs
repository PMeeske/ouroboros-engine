namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Configuration for the ConsolidatedMind.
/// </summary>
/// <param name="EnableThinking">Whether to use thinking mode when appropriate.</param>
/// <param name="EnableVerification">Whether to verify complex responses.</param>
/// <param name="EnableParallelExecution">Whether to run sub-models in parallel when possible.</param>
/// <param name="MaxParallelism">Maximum parallel model executions.</param>
/// <param name="DefaultTimeout">Default timeout for model calls.</param>
/// <param name="FallbackOnError">Whether to fallback to alternative models on error.</param>
public sealed record MindConfig(
    bool EnableThinking = true,
    bool EnableVerification = true,
    bool EnableParallelExecution = true,
    int MaxParallelism = 3,
    TimeSpan? DefaultTimeout = null,
    bool FallbackOnError = true)
{
    /// <summary>
    /// Creates a minimal configuration for resource-constrained environments.
    /// </summary>
    public static MindConfig Minimal() => new(
        EnableThinking: false,
        EnableVerification: false,
        EnableParallelExecution: false,
        MaxParallelism: 1,
        FallbackOnError: true);

    /// <summary>
    /// Creates a high-quality configuration for production use.
    /// </summary>
    public static MindConfig HighQuality() => new(
        EnableThinking: true,
        EnableVerification: true,
        EnableParallelExecution: true,
        MaxParallelism: 4,
        DefaultTimeout: TimeSpan.FromMinutes(5),
        FallbackOnError: true);
}
namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for orchestration observability.
/// </summary>
public sealed record OrchestrationObservabilityConfig(
    bool EnableTracing = true,
    bool EnableMetrics = true,
    bool EnableDetailedTags = false,
    double SamplingRate = 1.0);
namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Aggregated metrics for a variant.
/// </summary>
public sealed record VariantMetrics(
    double SuccessRate,
    double AverageLatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    double AverageConfidence,
    int TotalPrompts,
    int SuccessfulPrompts);
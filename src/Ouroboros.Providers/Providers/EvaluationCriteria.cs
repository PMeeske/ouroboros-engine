namespace Ouroboros.Providers;

/// <summary>
/// Criteria for evaluating response quality.
/// </summary>
public sealed record EvaluationCriteria(
    double RelevanceWeight = 0.3,
    double CoherenceWeight = 0.25,
    double CompletenessWeight = 0.2,
    double LatencyWeight = 0.15,
    double CostWeight = 0.1)
{
    public static EvaluationCriteria Default => new();
    public static EvaluationCriteria QualityFocused => new(0.4, 0.3, 0.2, 0.05, 0.05);
    public static EvaluationCriteria SpeedFocused => new(0.2, 0.2, 0.1, 0.4, 0.1);
    public static EvaluationCriteria CostFocused => new(0.2, 0.2, 0.1, 0.1, 0.4);
}
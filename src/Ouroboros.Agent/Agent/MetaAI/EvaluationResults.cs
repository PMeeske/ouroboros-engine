namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Aggregated evaluation results.
/// </summary>
public sealed record EvaluationResults(
    int TotalTests,
    int SuccessfulTests,
    int FailedTests,
    double AverageQualityScore,
    double AverageConfidence,
    TimeSpan AverageExecutionTime,
    List<EvaluationMetrics> TestResults,
    Dictionary<string, double> AggregatedMetrics);
namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Evaluation metrics for a single test case.
/// </summary>
public sealed record EvaluationMetrics(
    string TestCase,
    bool Success,
    double QualityScore,
    TimeSpan ExecutionTime,
    int PlanSteps,
    double ConfidenceScore,
    Dictionary<string, double> CustomMetrics);
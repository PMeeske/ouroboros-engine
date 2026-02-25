namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for self-evaluator behavior.
/// </summary>
public sealed record SelfEvaluatorConfig(
    int CalibrationSampleSize = 100,
    double MinConfidenceForPrediction = 0.3,
    int InsightGenerationBatchSize = 20,
    TimeSpan PerformanceAnalysisWindow = default)
{
    public SelfEvaluatorConfig() : this(
        100,
        0.3,
        20,
        TimeSpan.FromDays(7))
    {
    }
}
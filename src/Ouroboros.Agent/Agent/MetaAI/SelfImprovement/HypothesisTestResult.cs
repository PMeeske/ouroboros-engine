namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Result of hypothesis testing.
/// </summary>
public sealed record HypothesisTestResult(
    Hypothesis Hypothesis,
    Experiment Experiment,
    PlanExecutionResult Execution,
    bool HypothesisSupported,
    double ConfidenceAdjustment,
    string Explanation,
    DateTime TestedAt);
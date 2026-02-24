#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Ouroboros.Agent.MetaAI.Interpretability;

/// <summary>
/// Represents an explanation of an agent's decision.
/// </summary>
public sealed record DecisionExplanation(
    Guid DecisionId,
    string Summary,
    IReadOnlyList<ReasoningFactor> ContributingFactors,
    double OverallConfidence,
    DateTime Timestamp);

/// <summary>
/// A factor that contributed to a decision.
/// </summary>
public sealed record ReasoningFactor(
    string Source,
    string Description,
    double Weight,
    double Confidence);

/// <summary>
/// Explanation of a plan's reasoning.
/// </summary>
public sealed record PlanExplanation(
    string PlanGoal,
    IReadOnlyList<StepExplanation> StepExplanations,
    double OverallConfidence);

/// <summary>
/// Explanation of a single plan step.
/// </summary>
public sealed record StepExplanation(
    string Action,
    string Reasoning,
    IReadOnlyList<string> AlternativesConsidered);

/// <summary>
/// Report on what the agent is currently attending to.
/// </summary>
public sealed record AttentionReport(
    IReadOnlyList<AttentionItem> ActiveItems,
    int TotalWorkspaceSize,
    int HighPriorityCount);

/// <summary>
/// An item in the attention report.
/// </summary>
public sealed record AttentionItem(
    string Content,
    string Priority,
    string Source);

/// <summary>
/// Report on prediction calibration.
/// </summary>
public sealed record CalibrationReport(
    double BrierScore,
    double CalibrationError,
    int TotalForecasts,
    int VerifiedForecasts,
    int FailedForecasts);

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for adaptive planning capabilities.
/// </summary>
public interface IAdaptivePlanner
{
    /// <summary>
    /// Executes a plan with real-time adaptation.
    /// </summary>
    Task<Result<PlanExecutionResult, string>> ExecuteWithAdaptationAsync(
        Plan plan,
        AdaptivePlanningConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Registers a custom adaptation trigger.
    /// </summary>
    void RegisterTrigger(AdaptationTrigger trigger);

    /// <summary>
    /// Evaluates if adaptation is needed.
    /// </summary>
    Task<AdaptationAction?> EvaluateAdaptationAsync(
        ExecutionContext context,
        CancellationToken ct = default);
}
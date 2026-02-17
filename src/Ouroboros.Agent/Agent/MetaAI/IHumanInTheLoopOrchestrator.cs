namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for human-in-the-loop orchestration.
/// </summary>
public interface IHumanInTheLoopOrchestrator
{
    /// <summary>
    /// Executes a plan with human oversight.
    /// </summary>
    Task<Result<PlanExecutionResult, string>> ExecuteWithHumanOversightAsync(
        Plan plan,
        HumanInTheLoopConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Refines a plan interactively with human feedback.
    /// </summary>
    Task<Result<Plan, string>> RefinePlanInteractivelyAsync(
        Plan plan,
        CancellationToken ct = default);

    /// <summary>
    /// Sets the human feedback provider.
    /// </summary>
    void SetFeedbackProvider(IHumanFeedbackProvider provider);
}
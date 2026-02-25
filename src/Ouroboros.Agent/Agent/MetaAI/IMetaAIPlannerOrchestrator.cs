#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Meta-AI Layer v2 - Planner/Executor/Verifier Orchestrator
// Implements continual learning with plan-execute-verify loop
// Type definitions (Plan, PlanStep, StepResult, PlanExecutionResult,
// PlanVerificationResult, Skill) are in Ouroboros.Abstractions
// SelfImprovementModels.cs
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Core interface for Meta-AI v2 planner/executor/verifier orchestrator.
/// Implements continual learning through plan-execute-verify loop.
/// </summary>
public interface IMetaAIPlannerOrchestrator
{
    /// <summary>
    /// Plans how to accomplish a goal based on available tools and past experience.
    /// </summary>
    /// <param name="goal">The goal to accomplish</param>
    /// <param name="context">Additional context information</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A plan with steps and confidence scores</returns>
    Task<Result<Plan, string>> PlanAsync(
        string goal,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a plan step by step with monitoring.
    /// </summary>
    /// <param name="plan">The plan to execute</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Execution result with outcomes for each step</returns>
    Task<Result<PlanExecutionResult, string>> ExecuteAsync(
        Plan plan,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies execution results and provides feedback for improvement.
    /// </summary>
    /// <param name="execution">The execution result to verify</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Verification result with quality score and suggestions</returns>
    Task<Result<PlanVerificationResult, string>> VerifyAsync(
        PlanExecutionResult execution,
        CancellationToken ct = default);

    /// <summary>
    /// Learns from execution experience to improve future planning.
    /// </summary>
    /// <param name="verification">The verification result to learn from</param>
    void LearnFromExecution(PlanVerificationResult verification);

    /// <summary>
    /// Gets performance metrics for the orchestrator.
    /// </summary>
    IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics();
}

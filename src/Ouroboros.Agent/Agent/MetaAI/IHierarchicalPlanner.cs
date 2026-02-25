namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for hierarchical planning capabilities.
/// </summary>
public interface IHierarchicalPlanner
{
    /// <summary>
    /// Creates a hierarchical plan by decomposing complex tasks.
    /// </summary>
    Task<Result<HierarchicalPlan, string>> CreateHierarchicalPlanAsync(
        string goal,
        Dictionary<string, object>? context = null,
        HierarchicalPlanningConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a hierarchical plan recursively.
    /// </summary>
    Task<Result<PlanExecutionResult, string>> ExecuteHierarchicalAsync(
        HierarchicalPlan plan,
        CancellationToken ct = default);

    /// <summary>
    /// Creates an HTN hierarchical plan by decomposing abstract tasks using task networks.
    /// </summary>
    /// <param name="goal">The goal to achieve.</param>
    /// <param name="taskNetwork">Dictionary of task decompositions keyed by task name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An HTN hierarchical plan with abstract tasks and refinements.</returns>
    Task<Result<HtnHierarchicalPlan, string>> PlanHierarchicalAsync(
        string goal,
        Dictionary<string, TaskDecomposition> taskNetwork,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a temporal plan that satisfies the given temporal constraints.
    /// </summary>
    /// <param name="goal">The goal to achieve.</param>
    /// <param name="constraints">List of temporal constraints between tasks.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A temporal plan with scheduled tasks.</returns>
    Task<Result<TemporalPlan, string>> PlanWithConstraintsAsync(
        string goal,
        List<TemporalConstraint> constraints,
        CancellationToken ct = default);

    /// <summary>
    /// Repairs a broken plan using the specified repair strategy.
    /// </summary>
    /// <param name="brokenPlan">The plan that failed during execution.</param>
    /// <param name="trace">The execution trace showing what was executed and where it failed.</param>
    /// <param name="strategy">The repair strategy to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A repaired plan that addresses the failure.</returns>
    Task<Result<Plan, string>> RepairPlanAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        RepairStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Generates an explanation of a plan at the specified level of detail.
    /// </summary>
    /// <param name="plan">The plan to explain.</param>
    /// <param name="level">The level of detail for the explanation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A textual explanation of the plan.</returns>
    Task<Result<string, string>> ExplainPlanAsync(
        Plan plan,
        ExplanationLevel level,
        CancellationToken ct = default);
}
namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for cost-aware routing capabilities.
/// </summary>
public interface ICostAwareRouter
{
    /// <summary>
    /// Routes a task considering cost and quality tradeoffs.
    /// </summary>
    Task<Result<CostBenefitAnalysis, string>> RouteWithCostAwarenessAsync(
        string task,
        Dictionary<string, object>? context = null,
        CostAwareRoutingConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Estimates the cost of executing a plan.
    /// </summary>
    Task<double> EstimatePlanCostAsync(Plan plan, CancellationToken ct = default);

    /// <summary>
    /// Optimizes a plan to reduce cost while maintaining quality.
    /// </summary>
    Task<Result<Plan, string>> OptimizePlanForCostAsync(
        Plan plan,
        CostAwareRoutingConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Registers cost information for a resource.
    /// </summary>
    void RegisterCostInfo(CostInfo costInfo);
}
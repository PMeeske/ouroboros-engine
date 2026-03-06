using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Cognition.Planning;

/// <summary>
/// Kleisli arrow: Goal -> Result&lt;GoalDecomposition&gt;.
/// Splits a high-level goal into a structured plan of sub-goals,
/// routed through Hypergrid dimensional axes.
/// </summary>
public interface IGoalSplitter
{
    /// <summary>
    /// Decomposes a goal into a structured plan using Semantic Kernel
    /// planning and Hypergrid dimensional routing.
    /// </summary>
    Task<Result<GoalDecomposition, string>> SplitAsync(
        Goal goal,
        HypergridContext context,
        CancellationToken ct = default);
}

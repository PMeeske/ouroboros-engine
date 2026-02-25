using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Defines the contract for coordinating multiple agents to execute goals collaboratively.
/// </summary>
public interface IAgentCoordinator
{
    /// <summary>
    /// Gets the team of agents being coordinated.
    /// </summary>
    AgentTeam Team { get; }

    /// <summary>
    /// Executes a single goal by decomposing it into tasks and coordinating agents.
    /// </summary>
    /// <param name="goal">The goal to execute.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result containing the coordination outcome or an error message.</returns>
    Task<Result<CoordinationResult, string>> ExecuteAsync(Goal goal, CancellationToken ct = default);

    /// <summary>
    /// Executes multiple goals concurrently by coordinating agents in parallel.
    /// </summary>
    /// <param name="goals">The list of goals to execute.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result containing the coordination outcome or an error message.</returns>
    Task<Result<CoordinationResult, string>> ExecuteParallelAsync(IReadOnlyList<Goal> goals, CancellationToken ct = default);

    /// <summary>
    /// Sets the delegation strategy used for assigning tasks to agents.
    /// </summary>
    /// <param name="strategy">The delegation strategy to use.</param>
    void SetDelegationStrategy(IDelegationStrategy strategy);
}
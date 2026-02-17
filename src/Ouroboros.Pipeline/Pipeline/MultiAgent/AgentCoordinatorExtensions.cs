using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Provides extension methods for composing agent coordination with pipeline steps.
/// </summary>
public static class AgentCoordinatorExtensions
{
    /// <summary>
    /// Pipes a goal through the coordinator for execution.
    /// </summary>
    /// <param name="goalStep">The step that produces a goal.</param>
    /// <param name="coordinator">The agent coordinator to use for execution.</param>
    /// <returns>A step that produces the coordination result.</returns>
    public static Step<TInput, Result<CoordinationResult, string>> ThenCoordinate<TInput>(
        this Step<TInput, Goal> goalStep,
        IAgentCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(goalStep);
        ArgumentNullException.ThrowIfNull(coordinator);

        return async (TInput input) =>
        {
            Goal goal = await goalStep(input).ConfigureAwait(false);
            return await coordinator.ExecuteAsync(goal).ConfigureAwait(false);
        };
    }

    /// <summary>
    /// Pipes multiple goals through the coordinator for parallel execution.
    /// </summary>
    /// <param name="goalsStep">The step that produces a list of goals.</param>
    /// <param name="coordinator">The agent coordinator to use for execution.</param>
    /// <returns>A step that produces the coordination result.</returns>
    public static Step<TInput, Result<CoordinationResult, string>> ThenCoordinateParallel<TInput>(
        this Step<TInput, IReadOnlyList<Goal>> goalsStep,
        IAgentCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(goalsStep);
        ArgumentNullException.ThrowIfNull(coordinator);

        return async (TInput input) =>
        {
            IReadOnlyList<Goal> goals = await goalsStep(input).ConfigureAwait(false);
            return await coordinator.ExecuteParallelAsync(goals).ConfigureAwait(false);
        };
    }

    /// <summary>
    /// Creates an agent team from a collection of agent identities.
    /// </summary>
    /// <param name="identities">The agent identities to add to the team.</param>
    /// <returns>A new <see cref="AgentTeam"/> containing all specified agents.</returns>
    public static AgentTeam ToAgentTeam(this IEnumerable<AgentIdentity> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);

        AgentTeam team = AgentTeam.Empty;

        foreach (AgentIdentity identity in identities)
        {
            team = team.AddAgent(identity);
        }

        return team;
    }

    /// <summary>
    /// Filters a coordination result to include only successful tasks.
    /// </summary>
    /// <param name="result">The coordination result to filter.</param>
    /// <returns>A list of successfully completed tasks.</returns>
    public static IReadOnlyList<AgentTask> GetSuccessfulTasks(this CoordinationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Tasks
            .Where(t => t.Status == TaskStatus.Completed)
            .ToList();
    }

    /// <summary>
    /// Filters a coordination result to include only failed tasks.
    /// </summary>
    /// <param name="result">The coordination result to filter.</param>
    /// <returns>A list of failed tasks.</returns>
    public static IReadOnlyList<AgentTask> GetFailedTasks(this CoordinationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Tasks
            .Where(t => t.Status == TaskStatus.Failed)
            .ToList();
    }
}
namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Defines a strategy for delegating tasks to agents based on various criteria.
/// Implementations determine how agents are selected for task execution.
/// </summary>
public interface IDelegationStrategy
{
    /// <summary>
    /// Gets the name of this delegation strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Selects the most suitable agent for the given criteria from the team.
    /// </summary>
    /// <param name="criteria">The delegation criteria to match against.</param>
    /// <param name="team">The team of agents to select from.</param>
    /// <returns>A <see cref="DelegationResult"/> containing the selection outcome.</returns>
    DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team);

    /// <summary>
    /// Selects multiple suitable agents for the given criteria from the team.
    /// </summary>
    /// <param name="criteria">The delegation criteria to match against.</param>
    /// <param name="team">The team of agents to select from.</param>
    /// <param name="count">The maximum number of agents to select.</param>
    /// <returns>A read-only list of <see cref="DelegationResult"/> instances, ordered by match score.</returns>
    IReadOnlyList<DelegationResult> SelectAgents(DelegationCriteria criteria, AgentTeam team, int count);
}
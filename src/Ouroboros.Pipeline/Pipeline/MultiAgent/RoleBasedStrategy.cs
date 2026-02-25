namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// A delegation strategy that selects agents based on their role classification.
/// Falls back to capability matching if no agents match the preferred role.
/// </summary>
/// <remarks>
/// <para>Selection Algorithm:</para>
/// <list type="number">
///   <item>Filter agents matching the preferred role (if specified).</item>
///   <item>If no role match, fall back to all agents.</item>
///   <item>Score remaining agents by capability proficiency.</item>
///   <item>Apply role bonus (20%) for exact role matches.</item>
///   <item>Select agent with highest final score.</item>
/// </list>
/// </remarks>
public sealed class RoleBasedStrategy : IDelegationStrategy
{
    private const double RoleBonus = 0.20;
    private readonly CapabilityBasedStrategy _fallbackStrategy = new();

    /// <inheritdoc />
    public string Name => "RoleBased";

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents match the required role or capabilities.");
    }

    /// <inheritdoc />
    public IReadOnlyList<DelegationResult> SelectAgents(DelegationCriteria criteria, AgentTeam team, int count)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be greater than zero.");
        }

        if (!criteria.PreferredRole.HasValue)
        {
            // No role preference - delegate to capability-based strategy
            return _fallbackStrategy.SelectAgents(criteria, team, count);
        }

        AgentRole preferredRole = criteria.PreferredRole.Value;
        IReadOnlyList<AgentState> roleMatchedAgents = team.GetAgentsByRole(preferredRole);
        IReadOnlyList<AgentState> allAgents = team.GetAllAgents();

        List<(AgentState Agent, double Score, bool RoleMatch)> scoredAgents = new();

        // Score role-matched agents with bonus
        foreach (AgentState agent in roleMatchedAgents)
        {
            double baseScore = CalculateBaseScore(agent, criteria);
            double finalScore = Math.Min(1.0, baseScore + RoleBonus);

            if (finalScore >= criteria.MinProficiency)
            {
                scoredAgents.Add((agent, finalScore, true));
            }
        }

        // Score other agents without bonus as fallback candidates
        foreach (AgentState agent in allAgents)
        {
            if (agent.Identity.Role == preferredRole)
            {
                continue; // Already scored above
            }

            double score = CalculateBaseScore(agent, criteria);

            if (score >= criteria.MinProficiency)
            {
                scoredAgents.Add((agent, score, false));
            }
        }

        List<DelegationResult> results = scoredAgents
            .OrderByDescending(x => x.Score)
            .Take(count)
            .Select((x, index) =>
            {
                IReadOnlyList<Guid> alternatives = index == 0
                    ? scoredAgents
                        .OrderByDescending(a => a.Score)
                        .Skip(1)
                        .Take(3)
                        .Select(a => a.Agent.Identity.Id)
                        .ToList()
                    : Array.Empty<Guid>();

                string reasoning = x.RoleMatch
                    ? $"Selected '{x.Agent.Identity.Name}' with matching role '{preferredRole}'. Score: {x.Score:F2}"
                    : $"Selected '{x.Agent.Identity.Name}' as fallback (no role match). Score: {x.Score:F2}";

                return DelegationResult.Success(x.Agent.Identity.Id, reasoning, x.Score, alternatives);
            })
            .ToList();

        return results;
    }

    /// <summary>
    /// Calculates the base score for an agent considering capabilities and availability.
    /// </summary>
    private static double CalculateBaseScore(AgentState agent, DelegationCriteria criteria)
    {
        double score = agent.SuccessRate;

        if (criteria.RequiredCapabilities.Count > 0)
        {
            int matched = criteria.RequiredCapabilities.Count(c => agent.Identity.HasCapability(c));
            double coverage = (double)matched / criteria.RequiredCapabilities.Count;
            score = (score + coverage) / 2.0;
        }

        if (criteria.PreferAvailable && agent.IsAvailable)
        {
            score = Math.Min(1.0, score + 0.05);
        }

        return score;
    }
}
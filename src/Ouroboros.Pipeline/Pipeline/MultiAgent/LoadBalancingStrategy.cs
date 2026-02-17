namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// A delegation strategy that selects the least busy available agent.
/// Uses success rate as a tiebreaker when multiple agents have equal load.
/// </summary>
/// <remarks>
/// <para>Selection Algorithm:</para>
/// <list type="number">
///   <item>Filter agents that are currently available (idle).</item>
///   <item>If no available agents and not strictly required, consider all agents.</item>
///   <item>Sort by current task count (ascending) then success rate (descending).</item>
///   <item>Calculate load score: inverse of (completed + failed + current) normalized.</item>
///   <item>Select agent with lowest load and highest success rate.</item>
/// </list>
/// </remarks>
public sealed class LoadBalancingStrategy : IDelegationStrategy
{
    /// <inheritdoc />
    public string Name => "LoadBalancing";

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents available for load balancing.");
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

        IReadOnlyList<AgentState> candidates = criteria.PreferAvailable
            ? team.GetAvailableAgents()
            : team.GetAllAgents();

        // If no available agents but availability not strictly required, fall back to all
        if (candidates.Count == 0 && criteria.PreferAvailable)
        {
            candidates = team.GetAllAgents();
        }

        if (candidates.Count == 0)
        {
            return Array.Empty<DelegationResult>();
        }

        // Calculate max tasks for normalization
        int maxTasks = candidates.Max(a => a.CompletedTasks + a.FailedTasks);
        if (maxTasks == 0)
        {
            maxTasks = 1; // Avoid division by zero
        }

        List<(AgentState Agent, double Score)> scoredAgents = candidates
            .Select(agent =>
            {
                int totalTasks = agent.CompletedTasks + agent.FailedTasks;
                int currentLoad = agent.IsAvailable ? 0 : 1;

                // Load score: lower is better, so we invert
                // Score = (1 - normalized_load) * weight + success_rate * weight
                double loadFactor = 1.0 - ((double)(totalTasks + currentLoad) / (maxTasks + 1));
                double successFactor = agent.SuccessRate;

                // Weighted combination: 60% load balance, 40% success rate
                double score = (loadFactor * 0.6) + (successFactor * 0.4);

                return (Agent: agent, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        List<DelegationResult> results = scoredAgents
            .Take(count)
            .Select((x, index) =>
            {
                IReadOnlyList<Guid> alternatives = index == 0
                    ? scoredAgents
                        .Skip(1)
                        .Take(3)
                        .Select(a => a.Agent.Identity.Id)
                        .ToList()
                    : Array.Empty<Guid>();

                int totalTasks = x.Agent.CompletedTasks + x.Agent.FailedTasks;
                string availability = x.Agent.IsAvailable ? "available" : "busy";
                string reasoning = $"Selected '{x.Agent.Identity.Name}' ({availability}) with " +
                                   $"{totalTasks} completed tasks and {x.Agent.SuccessRate:P0} success rate. " +
                                   $"Load score: {x.Score:F2}";

                return DelegationResult.Success(x.Agent.Identity.Id, reasoning, x.Score, alternatives);
            })
            .ToList();

        return results;
    }
}
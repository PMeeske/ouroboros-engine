namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// A delegation strategy that selects agents based on their capability proficiency.
/// Scores agents by averaging their proficiency across required capabilities.
/// </summary>
/// <remarks>
/// <para>Scoring Algorithm:</para>
/// <list type="number">
///   <item>For each required capability, get the agent's proficiency (0.0 if missing).</item>
///   <item>Filter agents below the minimum proficiency threshold.</item>
///   <item>Calculate average proficiency across all required capabilities.</item>
///   <item>Apply availability bonus (10%) if agent is available and preference is set.</item>
///   <item>Select agent with highest final score.</item>
/// </list>
/// </remarks>
public sealed class CapabilityBasedStrategy : IDelegationStrategy
{
    private const double AvailabilityBonus = 0.10;

    /// <inheritdoc />
    public string Name => "CapabilityBased";

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents meet the capability requirements.");
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

        IReadOnlyList<AgentState> agents = team.GetAllAgents();

        if (agents.Count == 0)
        {
            return Array.Empty<DelegationResult>();
        }

        List<(AgentState Agent, double Score)> scoredAgents = new();

        foreach (AgentState agent in agents)
        {
            double score = CalculateCapabilityScore(agent, criteria);

            if (score >= criteria.MinProficiency)
            {
                // Apply availability bonus if preferred
                if (criteria.PreferAvailable && agent.IsAvailable)
                {
                    score = Math.Min(1.0, score + AvailabilityBonus);
                }

                scoredAgents.Add((agent, score));
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

                string reasoning = BuildCapabilityReasoning(x.Agent, criteria, x.Score);
                return DelegationResult.Success(x.Agent.Identity.Id, reasoning, x.Score, alternatives);
            })
            .ToList();

        return results;
    }

    /// <summary>
    /// Calculates the capability score for an agent based on required capabilities.
    /// </summary>
    /// <param name="agent">The agent to score.</param>
    /// <param name="criteria">The delegation criteria.</param>
    /// <returns>The capability score (0.0 to 1.0).</returns>
    private static double CalculateCapabilityScore(AgentState agent, DelegationCriteria criteria)
    {
        if (criteria.RequiredCapabilities.Count == 0)
        {
            // No specific capabilities required - return base score from success rate
            return agent.SuccessRate;
        }

        double totalProficiency = 0.0;
        int matchedCapabilities = 0;

        foreach (string capability in criteria.RequiredCapabilities)
        {
            double proficiency = agent.Identity.GetProficiencyFor(capability);
            if (proficiency > 0.0)
            {
                totalProficiency += proficiency;
                matchedCapabilities++;
            }
        }

        // Return weighted average: capability coverage * average proficiency
        double coverage = (double)matchedCapabilities / criteria.RequiredCapabilities.Count;
        double averageProficiency = matchedCapabilities > 0
            ? totalProficiency / matchedCapabilities
            : 0.0;

        return coverage * averageProficiency;
    }

    /// <summary>
    /// Builds a human-readable reasoning string for the capability-based selection.
    /// </summary>
    private static string BuildCapabilityReasoning(AgentState agent, DelegationCriteria criteria, double score)
    {
        if (criteria.RequiredCapabilities.Count == 0)
        {
            return $"Selected '{agent.Identity.Name}' based on success rate ({agent.SuccessRate:P0}). Score: {score:F2}";
        }

        int matchedCount = criteria.RequiredCapabilities
            .Count(c => agent.Identity.HasCapability(c));

        return $"Selected '{agent.Identity.Name}' matching {matchedCount}/{criteria.RequiredCapabilities.Count} " +
               $"required capabilities with score {score:F2}.";
    }
}
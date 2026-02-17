namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// A delegation strategy that combines capability, availability, and success rate
/// using a weighted scoring algorithm for optimal agent selection.
/// </summary>
/// <remarks>
/// <para>Weighted Scoring Algorithm:</para>
/// <list type="number">
///   <item>Capability Score (40%): Average proficiency across required capabilities.</item>
///   <item>Availability Score (25%): 1.0 if available, 0.3 if busy.</item>
///   <item>Success Rate Score (25%): Historical task success rate.</item>
///   <item>Role Match Score (10%): 1.0 if role matches, 0.5 otherwise.</item>
///   <item>Final Score = Σ(weight × component_score)</item>
/// </list>
/// </remarks>
public sealed class BestFitStrategy : IDelegationStrategy
{
    private const double CapabilityWeight = 0.40;
    private const double AvailabilityWeight = 0.25;
    private const double SuccessRateWeight = 0.25;
    private const double RoleMatchWeight = 0.10;

    /// <inheritdoc />
    public string Name => "BestFit";

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents meet the best-fit criteria.");
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

        List<(AgentState Agent, double Score, ScoreBreakdown Breakdown)> scoredAgents = agents
            .Select(agent => CalculateWeightedScore(agent, criteria))
            .Where(x => x.Score >= criteria.MinProficiency)
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

                string reasoning = BuildBestFitReasoning(x.Agent, x.Breakdown, x.Score);
                return DelegationResult.Success(x.Agent.Identity.Id, reasoning, x.Score, alternatives);
            })
            .ToList();

        return results;
    }

    /// <summary>
    /// Calculates the weighted score for an agent across all criteria dimensions.
    /// </summary>
    private static (AgentState Agent, double Score, ScoreBreakdown Breakdown) CalculateWeightedScore(
        AgentState agent,
        DelegationCriteria criteria)
    {
        // Calculate capability score
        double capabilityScore = CalculateCapabilityScore(agent, criteria);

        // Calculate availability score
        double availabilityScore = agent.IsAvailable ? 1.0 : 0.3;

        // Success rate is already 0.0 to 1.0
        double successRateScore = agent.SuccessRate;

        // Calculate role match score
        double roleMatchScore = criteria.PreferredRole.HasValue &&
                                agent.Identity.Role == criteria.PreferredRole.Value
            ? 1.0
            : 0.5;

        // Calculate weighted sum
        double totalScore =
            (capabilityScore * CapabilityWeight) +
            (availabilityScore * AvailabilityWeight) +
            (successRateScore * SuccessRateWeight) +
            (roleMatchScore * RoleMatchWeight);

        ScoreBreakdown breakdown = new(capabilityScore, availabilityScore, successRateScore, roleMatchScore);

        return (agent, totalScore, breakdown);
    }

    /// <summary>
    /// Calculates the capability score component.
    /// </summary>
    private static double CalculateCapabilityScore(AgentState agent, DelegationCriteria criteria)
    {
        if (criteria.RequiredCapabilities.Count == 0)
        {
            // No specific requirements - return average of all capability proficiencies
            ImmutableList<AgentCapability> capabilities = agent.Identity.Capabilities;
            return capabilities.Count > 0
                ? capabilities.Average(c => c.Proficiency)
                : 0.5; // Neutral score if no capabilities defined
        }

        double totalProficiency = 0.0;

        foreach (string capability in criteria.RequiredCapabilities)
        {
            totalProficiency += agent.Identity.GetProficiencyFor(capability);
        }

        return totalProficiency / criteria.RequiredCapabilities.Count;
    }

    /// <summary>
    /// Builds the reasoning string for best-fit selection.
    /// </summary>
    private static string BuildBestFitReasoning(AgentState agent, ScoreBreakdown breakdown, double totalScore)
    {
        return $"Selected '{agent.Identity.Name}' with best-fit score {totalScore:F2}. " +
               $"Breakdown: Capability={breakdown.Capability:F2}, Availability={breakdown.Availability:F2}, " +
               $"SuccessRate={breakdown.SuccessRate:F2}, RoleMatch={breakdown.RoleMatch:F2}";
    }

    /// <summary>
    /// Internal record for tracking score component breakdown.
    /// </summary>
    private readonly record struct ScoreBreakdown(
        double Capability,
        double Availability,
        double SuccessRate,
        double RoleMatch);
}
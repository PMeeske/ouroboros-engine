namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// A delegation strategy that combines multiple strategies with configurable weights.
/// Aggregates scores from each strategy to produce a final selection.
/// </summary>
/// <remarks>
/// <para>Composite Algorithm:</para>
/// <list type="number">
///   <item>Execute each child strategy independently.</item>
///   <item>For each agent, collect scores from all strategies that selected it.</item>
///   <item>Calculate weighted average: Σ(strategy_weight × strategy_score) / Σ(weights)</item>
///   <item>Select agent with highest composite score.</item>
/// </list>
/// </remarks>
public sealed class CompositeStrategy : IDelegationStrategy
{
    private readonly IReadOnlyList<(IDelegationStrategy Strategy, double Weight)> _strategies;

    /// <inheritdoc />
    public string Name => "Composite";

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeStrategy"/> class.
    /// </summary>
    /// <param name="strategies">The weighted strategies to combine.</param>
    private CompositeStrategy(IReadOnlyList<(IDelegationStrategy Strategy, double Weight)> strategies)
    {
        _strategies = strategies;
    }

    /// <summary>
    /// Creates a new composite strategy from the specified weighted strategies.
    /// </summary>
    /// <param name="strategies">The strategies and their weights.</param>
    /// <returns>A new <see cref="CompositeStrategy"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strategies"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no strategies are provided.</exception>
    public static CompositeStrategy Create(params (IDelegationStrategy Strategy, double Weight)[] strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        if (strategies.Length == 0)
        {
            throw new ArgumentException("At least one strategy must be provided.", nameof(strategies));
        }

        foreach ((IDelegationStrategy strategy, double weight) in strategies)
        {
            ArgumentNullException.ThrowIfNull(strategy);

            if (weight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(strategies),
                    weight,
                    $"Strategy weight must be positive. Got {weight} for {strategy.Name}.");
            }
        }

        return new CompositeStrategy(strategies.ToList());
    }

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents selected by composite strategy.");
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

        // Collect results from all strategies
        Dictionary<Guid, List<(double Score, double Weight, string StrategyName)>> agentScores = new();
        double totalWeight = _strategies.Sum(s => s.Weight);

        foreach ((IDelegationStrategy strategy, double weight) in _strategies)
        {
            DelegationResult result = strategy.SelectAgent(criteria, team);

            if (result.HasMatch && result.SelectedAgentId.HasValue)
            {
                Guid agentId = result.SelectedAgentId.Value;

                if (!agentScores.ContainsKey(agentId))
                {
                    agentScores[agentId] = new List<(double, double, string)>();
                }

                agentScores[agentId].Add((result.MatchScore, weight, strategy.Name));
            }

            // Also consider alternatives
            foreach (Guid altId in result.Alternatives)
            {
                if (!agentScores.ContainsKey(altId))
                {
                    agentScores[altId] = new List<(double, double, string)>();
                }

                // Alternatives get a reduced score (50% of primary)
                agentScores[altId].Add((result.MatchScore * 0.5, weight * 0.5, strategy.Name));
            }
        }

        if (agentScores.Count == 0)
        {
            return Array.Empty<DelegationResult>();
        }

        // Calculate composite scores
        List<(Guid AgentId, double CompositeScore, string Contributions)> compositeScores = agentScores
            .Select(kvp =>
            {
                double weightedSum = kvp.Value.Sum(s => s.Score * s.Weight);
                double appliedWeight = kvp.Value.Sum(s => s.Weight);
                double compositeScore = weightedSum / totalWeight;

                string contributions = string.Join(", ",
                    kvp.Value.Select(s => $"{s.StrategyName}:{s.Score:F2}"));

                return (AgentId: kvp.Key, CompositeScore: compositeScore, Contributions: contributions);
            })
            .Where(x => x.CompositeScore >= criteria.MinProficiency)
            .OrderByDescending(x => x.CompositeScore)
            .ToList();

        List<DelegationResult> results = compositeScores
            .Take(count)
            .Select((x, index) =>
            {
                IReadOnlyList<Guid> alternatives = index == 0
                    ? compositeScores
                        .Skip(1)
                        .Take(3)
                        .Select(a => a.AgentId)
                        .ToList()
                    : Array.Empty<Guid>();

                string reasoning = $"Composite selection with score {x.CompositeScore:F2}. " +
                                   $"Strategy contributions: [{x.Contributions}]";

                return DelegationResult.Success(x.AgentId, reasoning, x.CompositeScore, alternatives);
            })
            .ToList();

        return results;
    }
}
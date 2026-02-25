namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// A delegation strategy that cycles through agents in round-robin order.
/// Maintains internal state to ensure fair distribution of tasks across agents.
/// </summary>
/// <remarks>
/// <para>Selection Algorithm:</para>
/// <list type="number">
///   <item>Maintain a rotating index across invocations.</item>
///   <item>Filter candidates based on availability preference.</item>
///   <item>Select agent at current index and advance.</item>
///   <item>Score is based on position in rotation (first = 1.0, decreasing).</item>
///   <item>Thread-safe through internal locking.</item>
/// </list>
/// </remarks>
public sealed class RoundRobinStrategy : IDelegationStrategy
{
    private int _currentIndex;
    private readonly object _lock = new();

    /// <inheritdoc />
    public string Name => "RoundRobin";

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents available for round-robin selection.");
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

        // Fall back to all agents if no available ones
        if (candidates.Count == 0 && criteria.PreferAvailable)
        {
            candidates = team.GetAllAgents();
        }

        if (candidates.Count == 0)
        {
            return Array.Empty<DelegationResult>();
        }

        List<DelegationResult> results = new();
        int actualCount = Math.Min(count, candidates.Count);

        lock (_lock)
        {
            for (int i = 0; i < actualCount; i++)
            {
                int index = (_currentIndex + i) % candidates.Count;
                AgentState agent = candidates[index];

                // Score decreases based on position in selection order
                double score = 1.0 - ((double)i / actualCount);

                IReadOnlyList<Guid> alternatives = i == 0 && candidates.Count > 1
                    ? Enumerable.Range(1, Math.Min(3, candidates.Count - 1))
                        .Select(j => candidates[(_currentIndex + j) % candidates.Count].Identity.Id)
                        .ToList()
                    : Array.Empty<Guid>();

                string reasoning = $"Selected '{agent.Identity.Name}' in round-robin position {index + 1}/{candidates.Count}.";
                results.Add(DelegationResult.Success(agent.Identity.Id, reasoning, score, alternatives));
            }

            // Advance the index for next selection
            _currentIndex = (_currentIndex + actualCount) % candidates.Count;
        }

        return results;
    }

    /// <summary>
    /// Resets the round-robin index to start from the first agent.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _currentIndex = 0;
        }
    }
}
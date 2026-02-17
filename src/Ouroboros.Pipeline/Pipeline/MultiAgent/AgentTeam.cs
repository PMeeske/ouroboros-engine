namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents a team of agents that can be coordinated to work on tasks.
/// The team is immutable and provides methods for querying and managing agents.
/// </summary>
public sealed class AgentTeam
{
    private readonly ImmutableDictionary<Guid, AgentState> _agents;

    /// <summary>
    /// Gets an empty agent team with no agents.
    /// </summary>
    public static AgentTeam Empty { get; } = new AgentTeam(ImmutableDictionary<Guid, AgentState>.Empty);

    /// <summary>
    /// Gets the number of agents in the team.
    /// </summary>
    public int Count => _agents.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentTeam"/> class.
    /// </summary>
    /// <param name="agents">The dictionary of agents in the team.</param>
    private AgentTeam(ImmutableDictionary<Guid, AgentState> agents)
    {
        _agents = agents;
    }

    /// <summary>
    /// Creates a new team with the specified agent added.
    /// </summary>
    /// <param name="identity">The identity of the agent to add.</param>
    /// <returns>A new <see cref="AgentTeam"/> with the agent added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
    public AgentTeam AddAgent(AgentIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        AgentState state = AgentState.ForAgent(identity);
        return new AgentTeam(_agents.SetItem(identity.Id, state));
    }

    /// <summary>
    /// Creates a new team with the specified agent removed.
    /// </summary>
    /// <param name="agentId">The ID of the agent to remove.</param>
    /// <returns>A new <see cref="AgentTeam"/> without the specified agent.</returns>
    public AgentTeam RemoveAgent(Guid agentId)
    {
        return new AgentTeam(_agents.Remove(agentId));
    }

    /// <summary>
    /// Gets the state of an agent by ID.
    /// </summary>
    /// <param name="agentId">The ID of the agent to retrieve.</param>
    /// <returns>An <see cref="Option{T}"/> containing the agent state if found.</returns>
    public Option<AgentState> GetAgent(Guid agentId)
    {
        if (_agents.TryGetValue(agentId, out AgentState? state))
        {
            return Option<AgentState>.Some(state);
        }

        return Option<AgentState>.None();
    }

    /// <summary>
    /// Gets all agents that are currently available to accept new tasks.
    /// </summary>
    /// <returns>A read-only list of available agent states.</returns>
    public IReadOnlyList<AgentState> GetAvailableAgents()
    {
        return _agents.Values
            .Where(a => a.IsAvailable)
            .ToList();
    }

    /// <summary>
    /// Gets all agents that have the specified capability.
    /// </summary>
    /// <param name="capability">The name of the capability to search for.</param>
    /// <returns>A read-only list of agent states with the specified capability.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="capability"/> is null.</exception>
    public IReadOnlyList<AgentState> GetAgentsWithCapability(string capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        return _agents.Values
            .Where(a => a.Identity.HasCapability(capability))
            .ToList();
    }

    /// <summary>
    /// Gets all agents that have the specified role.
    /// </summary>
    /// <param name="role">The role to search for.</param>
    /// <returns>A read-only list of agent states with the specified role.</returns>
    public IReadOnlyList<AgentState> GetAgentsByRole(AgentRole role)
    {
        return _agents.Values
            .Where(a => a.Identity.Role == role)
            .ToList();
    }

    /// <summary>
    /// Gets all agent states in the team.
    /// </summary>
    /// <returns>A read-only list of all agent states.</returns>
    public IReadOnlyList<AgentState> GetAllAgents()
    {
        return _agents.Values.ToList();
    }

    /// <summary>
    /// Updates the state of an agent in the team.
    /// </summary>
    /// <param name="agentId">The ID of the agent to update.</param>
    /// <param name="newState">The new state for the agent.</param>
    /// <returns>A new <see cref="AgentTeam"/> with the updated agent state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="newState"/> is null.</exception>
    internal AgentTeam UpdateAgent(Guid agentId, AgentState newState)
    {
        ArgumentNullException.ThrowIfNull(newState);

        if (!_agents.ContainsKey(agentId))
        {
            return this;
        }

        return new AgentTeam(_agents.SetItem(agentId, newState));
    }

    /// <summary>
    /// Gets the identity dictionary for all agents in the team.
    /// </summary>
    /// <returns>A read-only dictionary mapping agent IDs to identities.</returns>
    internal IReadOnlyDictionary<Guid, AgentIdentity> GetIdentityDictionary()
    {
        return _agents.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Identity);
    }
}
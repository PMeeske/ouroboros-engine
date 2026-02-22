namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Manages a voting session for multi-agent consensus decisions.
/// </summary>
public sealed class VotingSession
{
    private readonly Guid _sessionId;
    private readonly string _topic;
    private readonly IReadOnlyList<string> _options;
    private readonly IConsensusProtocol _protocol;
    private readonly List<AgentVote> _votes;
    private readonly HashSet<Guid> _votedAgents;
    private readonly object syncLock = new object();

    /// <summary>
    /// Gets the unique identifier for this voting session.
    /// </summary>
    public Guid SessionId => _sessionId;

    /// <summary>
    /// Gets the topic being voted on.
    /// </summary>
    public string Topic => _topic;

    /// <summary>
    /// Gets the available options for voting.
    /// </summary>
    public IReadOnlyList<string> Options => _options;

    /// <summary>
    /// Gets the number of votes cast so far.
    /// </summary>
    public int VoteCount
    {
        get
        {
            lock (syncLock)
            {
                return _votes.Count;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VotingSession"/> class.
    /// </summary>
    /// <param name="sessionId">The unique session identifier.</param>
    /// <param name="topic">The topic being voted on.</param>
    /// <param name="options">The available voting options.</param>
    /// <param name="protocol">The consensus protocol to use.</param>
    private VotingSession(Guid sessionId, string topic, IReadOnlyList<string> options, IConsensusProtocol protocol)
    {
        _sessionId = sessionId;
        _topic = topic;
        _options = options;
        _protocol = protocol;
        _votes = new List<AgentVote>();
        _votedAgents = new HashSet<Guid>();
    }

    /// <summary>
    /// Creates a new voting session.
    /// </summary>
    /// <param name="topic">The topic being voted on.</param>
    /// <param name="options">The available voting options.</param>
    /// <param name="protocol">The consensus protocol to use.</param>
    /// <returns>A new <see cref="VotingSession"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when options is empty.</exception>
    public static VotingSession Create(string topic, IReadOnlyList<string> options, IConsensusProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(protocol);

        if (options.Count == 0)
        {
            throw new ArgumentException("At least one option must be provided.", nameof(options));
        }

        return new VotingSession(Guid.NewGuid(), topic, options.ToImmutableList(), protocol);
    }

    /// <summary>
    /// Casts a vote in this session.
    /// </summary>
    /// <param name="vote">The vote to cast.</param>
    /// <exception cref="ArgumentNullException">Thrown when vote is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when agent has already voted or option is invalid.</exception>
    public void CastVote(AgentVote vote)
    {
        ArgumentNullException.ThrowIfNull(vote);

        lock (syncLock)
        {
            if (_votedAgents.Contains(vote.AgentId))
            {
                throw new InvalidOperationException($"Agent {vote.AgentId} has already voted in this session.");
            }

            bool isValidOption = false;
            foreach (string option in _options)
            {
                if (string.Equals(option, vote.Option, StringComparison.Ordinal))
                {
                    isValidOption = true;
                    break;
                }
            }

            if (!isValidOption)
            {
                throw new InvalidOperationException($"Option '{vote.Option}' is not a valid option for this session.");
            }

            _votes.Add(vote);
            _votedAgents.Add(vote.AgentId);
        }
    }

    /// <summary>
    /// Checks if an agent has already voted.
    /// </summary>
    /// <param name="agentId">The agent identifier to check.</param>
    /// <returns>True if the agent has voted; otherwise, false.</returns>
    public bool HasVoted(Guid agentId)
    {
        lock (syncLock)
        {
            return _votedAgents.Contains(agentId);
        }
    }

    /// <summary>
    /// Attempts to get the consensus result if available.
    /// </summary>
    /// <returns>Some with the result if consensus is reached; otherwise, None.</returns>
    public Option<ConsensusResult> TryGetResult()
    {
        ConsensusResult result = GetResult();

        if (result.HasConsensus)
        {
            return Option<ConsensusResult>.Some(result);
        }

        return Option<ConsensusResult>.None();
    }

    /// <summary>
    /// Gets the current consensus result.
    /// </summary>
    /// <returns>The current consensus result.</returns>
    public ConsensusResult GetResult()
    {
        IReadOnlyList<AgentVote> currentVotes;

        lock (syncLock)
        {
            currentVotes = _votes.ToImmutableList();
        }

        return _protocol.Evaluate(currentVotes);
    }

    /// <summary>
    /// Gets a snapshot of all votes cast in this session.
    /// </summary>
    /// <returns>An immutable list of all votes.</returns>
    public IReadOnlyList<AgentVote> GetVotes()
    {
        lock (syncLock)
        {
            return _votes.ToImmutableList();
        }
    }
}
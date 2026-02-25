namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents the result of a delegation attempt, including the selected agent and match quality.
/// </summary>
/// <param name="SelectedAgentId">The ID of the selected agent, or null if no match was found.</param>
/// <param name="Reasoning">A human-readable explanation of the delegation decision.</param>
/// <param name="MatchScore">A score indicating how well the agent matches the criteria (0.0 to 1.0).</param>
/// <param name="Alternatives">A list of alternative agent IDs that could also handle the task.</param>
public sealed record DelegationResult(
    Guid? SelectedAgentId,
    string Reasoning,
    double MatchScore,
    IReadOnlyList<Guid> Alternatives)
{
    /// <summary>
    /// Gets a value indicating whether a matching agent was found.
    /// </summary>
    /// <value><c>true</c> if an agent was selected; otherwise, <c>false</c>.</value>
    public bool HasMatch => SelectedAgentId.HasValue;

    /// <summary>
    /// Creates a successful delegation result with the specified agent.
    /// </summary>
    /// <param name="agentId">The ID of the selected agent.</param>
    /// <param name="reasoning">The reasoning for the selection.</param>
    /// <param name="score">The match score (0.0 to 1.0).</param>
    /// <param name="alternatives">Optional list of alternative agent IDs.</param>
    /// <returns>A new <see cref="DelegationResult"/> indicating a successful match.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reasoning"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="score"/> is not between 0.0 and 1.0.
    /// </exception>
    public static DelegationResult Success(
        Guid agentId,
        string reasoning,
        double score,
        IReadOnlyList<Guid>? alternatives = null)
    {
        ArgumentNullException.ThrowIfNull(reasoning);

        if (score < 0.0 || score > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "Score must be between 0.0 and 1.0.");
        }

        return new DelegationResult(
            SelectedAgentId: agentId,
            Reasoning: reasoning,
            MatchScore: score,
            Alternatives: alternatives ?? Array.Empty<Guid>());
    }

    /// <summary>
    /// Creates a delegation result indicating no suitable agent was found.
    /// </summary>
    /// <param name="reason">The reason why no agent could be selected.</param>
    /// <returns>A new <see cref="DelegationResult"/> indicating no match.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reason"/> is null.</exception>
    public static DelegationResult NoMatch(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return new DelegationResult(
            SelectedAgentId: null,
            Reasoning: reason,
            MatchScore: 0.0,
            Alternatives: Array.Empty<Guid>());
    }
}
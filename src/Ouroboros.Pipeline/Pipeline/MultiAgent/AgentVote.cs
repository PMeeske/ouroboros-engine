namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents a vote cast by an agent in a consensus decision.
/// </summary>
/// <param name="AgentId">The unique identifier of the voting agent.</param>
/// <param name="Option">The option being voted for.</param>
/// <param name="Confidence">The confidence level of the vote (0.0 to 1.0).</param>
/// <param name="Reasoning">Optional reasoning for the vote.</param>
/// <param name="Timestamp">When the vote was cast.</param>
public sealed record AgentVote(
    Guid AgentId,
    string Option,
    double Confidence,
    string? Reasoning,
    DateTime Timestamp)
{
    /// <summary>
    /// Creates a new agent vote with the current timestamp.
    /// </summary>
    /// <param name="agentId">The unique identifier of the voting agent.</param>
    /// <param name="option">The option being voted for.</param>
    /// <param name="confidence">The confidence level of the vote (0.0 to 1.0).</param>
    /// <param name="reasoning">Optional reasoning for the vote.</param>
    /// <returns>A new <see cref="AgentVote"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when option is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when confidence is not between 0.0 and 1.0.</exception>
    public static AgentVote Create(Guid agentId, string option, double confidence, string? reasoning = null)
    {
        ArgumentNullException.ThrowIfNull(option);

        if (confidence < 0.0 || confidence > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be between 0.0 and 1.0.");
        }

        return new AgentVote(agentId, option, confidence, reasoning, DateTime.UtcNow);
    }
}
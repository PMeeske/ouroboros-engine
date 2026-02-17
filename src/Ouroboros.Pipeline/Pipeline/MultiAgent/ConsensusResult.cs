namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents the result of a consensus evaluation.
/// </summary>
/// <param name="WinningOption">The option that won the consensus (empty if no consensus).</param>
/// <param name="AggregateConfidence">The aggregate confidence in the winning option.</param>
/// <param name="AllVotes">All votes cast in the evaluation.</param>
/// <param name="VoteCounts">Count of votes per option.</param>
/// <param name="ConfidenceByOption">Aggregate confidence per option.</param>
/// <param name="HasConsensus">Whether consensus was reached.</param>
/// <param name="Protocol">The protocol used to evaluate consensus.</param>
public sealed record ConsensusResult(
    string WinningOption,
    double AggregateConfidence,
    IReadOnlyList<AgentVote> AllVotes,
    IReadOnlyDictionary<string, int> VoteCounts,
    IReadOnlyDictionary<string, double> ConfidenceByOption,
    bool HasConsensus,
    string Protocol)
{
    /// <summary>
    /// Gets the total number of votes cast.
    /// </summary>
    public int TotalVotes => AllVotes.Count;

    /// <summary>
    /// Calculates the participation rate as a percentage.
    /// </summary>
    /// <param name="totalAgents">The total number of agents eligible to vote.</param>
    /// <returns>The participation rate between 0.0 and 1.0.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when totalAgents is less than or equal to zero.</exception>
    public double ParticipationRate(int totalAgents)
    {
        if (totalAgents <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAgents), totalAgents, "Total agents must be greater than zero.");
        }

        return (double)TotalVotes / totalAgents;
    }

    /// <summary>
    /// Creates a result indicating no consensus was reached.
    /// </summary>
    /// <param name="votes">The votes that were cast.</param>
    /// <param name="protocol">The protocol that was used.</param>
    /// <returns>A <see cref="ConsensusResult"/> indicating no consensus.</returns>
    public static ConsensusResult NoConsensus(IReadOnlyList<AgentVote> votes, string protocol)
    {
        ArgumentNullException.ThrowIfNull(votes);
        ArgumentNullException.ThrowIfNull(protocol);

        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        return new ConsensusResult(
            WinningOption: string.Empty,
            AggregateConfidence: 0.0,
            AllVotes: votes,
            VoteCounts: voteCounts,
            ConfidenceByOption: confidenceByOption,
            HasConsensus: false,
            Protocol: protocol);
    }

    /// <summary>
    /// Calculates vote counts per option.
    /// </summary>
    private static Dictionary<string, int> CalculateVoteCounts(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (AgentVote vote in votes)
        {
            if (counts.TryGetValue(vote.Option, out int count))
            {
                counts[vote.Option] = count + 1;
            }
            else
            {
                counts[vote.Option] = 1;
            }
        }

        return counts;
    }

    /// <summary>
    /// Calculates aggregate confidence per option.
    /// </summary>
    private static Dictionary<string, double> CalculateConfidenceByOption(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, double> confidence = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (AgentVote vote in votes)
        {
            if (confidence.TryGetValue(vote.Option, out double existing))
            {
                confidence[vote.Option] = existing + vote.Confidence;
            }
            else
            {
                confidence[vote.Option] = vote.Confidence;
            }
        }

        return confidence;
    }
}
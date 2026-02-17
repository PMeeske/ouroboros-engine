// <copyright file="ConsensusProtocol.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Implements consensus mechanisms for multi-agent decision making.
/// </summary>
public sealed class ConsensusProtocol : IConsensusProtocol
{
    private readonly ConsensusStrategy strategy;
    private readonly double customThreshold;

    private const double MajorityThreshold = 0.5;
    private const double SuperMajorityThreshold = 0.666666666666667;
    private const double UnanimousThreshold = 1.0;

    /// <summary>
    /// Gets a protocol using simple majority voting.
    /// </summary>
    public static ConsensusProtocol Majority { get; } = new ConsensusProtocol(ConsensusStrategy.Majority, MajorityThreshold);

    /// <summary>
    /// Gets a protocol using super majority voting (66%+).
    /// </summary>
    public static ConsensusProtocol SuperMajority { get; } = new ConsensusProtocol(ConsensusStrategy.SuperMajority, SuperMajorityThreshold);

    /// <summary>
    /// Gets a protocol requiring unanimous agreement.
    /// </summary>
    public static ConsensusProtocol Unanimous { get; } = new ConsensusProtocol(ConsensusStrategy.Unanimous, UnanimousThreshold);

    /// <summary>
    /// Gets a protocol using confidence-weighted voting.
    /// </summary>
    public static ConsensusProtocol WeightedByConfidence { get; } = new ConsensusProtocol(ConsensusStrategy.WeightedByConfidence, MajorityThreshold);

    /// <summary>
    /// Gets a protocol selecting the highest confidence vote.
    /// </summary>
    public static ConsensusProtocol HighestConfidence { get; } = new ConsensusProtocol(ConsensusStrategy.HighestConfidence, 0.0);

    /// <summary>
    /// Gets the consensus strategy used by this protocol.
    /// </summary>
    public ConsensusStrategy Strategy => strategy;

    /// <summary>
    /// Gets the threshold used for consensus determination.
    /// </summary>
    public double Threshold => customThreshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsensusProtocol"/> class.
    /// </summary>
    /// <param name="strategy">The consensus strategy to use.</param>
    /// <param name="threshold">The threshold for consensus (0.0 to 1.0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when threshold is invalid.</exception>
    public ConsensusProtocol(ConsensusStrategy strategy, double threshold = 0.5)
    {
        if (threshold < 0.0 || threshold > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Threshold must be between 0.0 and 1.0.");
        }

        this.strategy = strategy;
        this.customThreshold = threshold;
    }

    /// <summary>
    /// Evaluates the given votes and determines if consensus is reached.
    /// </summary>
    /// <param name="votes">The votes to evaluate.</param>
    /// <returns>The consensus result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when votes is null.</exception>
    public ConsensusResult Evaluate(IReadOnlyList<AgentVote> votes)
    {
        ArgumentNullException.ThrowIfNull(votes);

        if (votes.Count == 0)
        {
            return ConsensusResult.NoConsensus(votes, strategy.ToString());
        }

        return strategy switch
        {
            ConsensusStrategy.Majority => EvaluateMajority(votes),
            ConsensusStrategy.SuperMajority => EvaluateSuperMajority(votes),
            ConsensusStrategy.Unanimous => EvaluateUnanimous(votes),
            ConsensusStrategy.WeightedByConfidence => EvaluateWeightedByConfidence(votes),
            ConsensusStrategy.HighestConfidence => EvaluateHighestConfidence(votes),
            ConsensusStrategy.RankedChoice => EvaluateRankedChoice(votes),
            _ => ConsensusResult.NoConsensus(votes, strategy.ToString()),
        };
    }

    /// <summary>
    /// Determines if the votes meet a specified threshold.
    /// </summary>
    /// <param name="votes">The votes to evaluate.</param>
    /// <param name="threshold">The threshold to meet (0.0 to 1.0).</param>
    /// <returns>True if threshold is met; otherwise, false.</returns>
    public bool MeetsThreshold(IReadOnlyList<AgentVote> votes, double threshold)
    {
        ArgumentNullException.ThrowIfNull(votes);

        if (votes.Count == 0)
        {
            return false;
        }

        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        int maxVotes = voteCounts.Values.Max();
        double ratio = (double)maxVotes / votes.Count;

        return ratio > threshold;
    }

    /// <summary>
    /// Evaluates votes using simple majority (greater than 50%).
    /// </summary>
    private ConsensusResult EvaluateMajority(IReadOnlyList<AgentVote> votes)
    {
        return EvaluateThresholdBased(votes, MajorityThreshold, nameof(ConsensusStrategy.Majority));
    }

    /// <summary>
    /// Evaluates votes using super majority (greater than 66%).
    /// </summary>
    private ConsensusResult EvaluateSuperMajority(IReadOnlyList<AgentVote> votes)
    {
        return EvaluateThresholdBased(votes, SuperMajorityThreshold, nameof(ConsensusStrategy.SuperMajority));
    }

    /// <summary>
    /// Evaluates votes requiring unanimous agreement.
    /// </summary>
    private ConsensusResult EvaluateUnanimous(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        // Unanimous requires all votes for the same option
        if (voteCounts.Count == 1)
        {
            KeyValuePair<string, int> winner = voteCounts.First();
            double avgConfidence = confidenceByOption[winner.Key] / winner.Value;

            return new ConsensusResult(
                WinningOption: winner.Key,
                AggregateConfidence: avgConfidence,
                AllVotes: votes,
                VoteCounts: voteCounts,
                ConfidenceByOption: confidenceByOption,
                HasConsensus: true,
                Protocol: nameof(ConsensusStrategy.Unanimous));
        }

        return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.Unanimous));
    }

    /// <summary>
    /// Evaluates votes using confidence-weighted voting.
    /// </summary>
    private ConsensusResult EvaluateWeightedByConfidence(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        double totalConfidence = votes.Sum(v => v.Confidence);

        if (totalConfidence <= 0.0)
        {
            return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.WeightedByConfidence));
        }

        // Find the option with the highest weighted confidence
        string? winningOption = null;
        double maxWeightedConfidence = 0.0;

        foreach (KeyValuePair<string, double> kvp in confidenceByOption)
        {
            double weightedRatio = kvp.Value / totalConfidence;
            if (weightedRatio > maxWeightedConfidence)
            {
                maxWeightedConfidence = weightedRatio;
                winningOption = kvp.Key;
            }
        }

        if (winningOption == null || maxWeightedConfidence <= customThreshold)
        {
            return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.WeightedByConfidence));
        }

        return new ConsensusResult(
            WinningOption: winningOption,
            AggregateConfidence: maxWeightedConfidence,
            AllVotes: votes,
            VoteCounts: voteCounts,
            ConfidenceByOption: confidenceByOption,
            HasConsensus: true,
            Protocol: nameof(ConsensusStrategy.WeightedByConfidence));
    }

    /// <summary>
    /// Evaluates votes selecting the single highest confidence vote.
    /// </summary>
    private ConsensusResult EvaluateHighestConfidence(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        // Find the vote with the highest individual confidence
        AgentVote? highestConfidenceVote = null;
        double maxConfidence = -1.0;

        foreach (AgentVote vote in votes)
        {
            if (vote.Confidence > maxConfidence)
            {
                maxConfidence = vote.Confidence;
                highestConfidenceVote = vote;
            }
        }

        if (highestConfidenceVote == null)
        {
            return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.HighestConfidence));
        }

        return new ConsensusResult(
            WinningOption: highestConfidenceVote.Option,
            AggregateConfidence: highestConfidenceVote.Confidence,
            AllVotes: votes,
            VoteCounts: voteCounts,
            ConfidenceByOption: confidenceByOption,
            HasConsensus: true,
            Protocol: nameof(ConsensusStrategy.HighestConfidence));
    }

    /// <summary>
    /// Evaluates votes using ranked choice voting with elimination.
    /// </summary>
    private ConsensusResult EvaluateRankedChoice(IReadOnlyList<AgentVote> votes)
    {
        // For ranked choice, we simulate elimination rounds
        // Since we have single votes per agent, we use confidence as tiebreaker
        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        if (voteCounts.Count == 0)
        {
            return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.RankedChoice));
        }

        // Check if any option has majority
        int totalVotes = votes.Count;
        foreach (KeyValuePair<string, int> kvp in voteCounts)
        {
            double ratio = (double)kvp.Value / totalVotes;
            if (ratio > MajorityThreshold)
            {
                double avgConfidence = confidenceByOption[kvp.Key] / kvp.Value;
                return new ConsensusResult(
                    WinningOption: kvp.Key,
                    AggregateConfidence: avgConfidence,
                    AllVotes: votes,
                    VoteCounts: voteCounts,
                    ConfidenceByOption: confidenceByOption,
                    HasConsensus: true,
                    Protocol: nameof(ConsensusStrategy.RankedChoice));
            }
        }

        // If no majority, select option with highest confidence-weighted votes
        string? bestOption = null;
        double bestScore = -1.0;

        foreach (KeyValuePair<string, double> kvp in confidenceByOption)
        {
            if (kvp.Value > bestScore)
            {
                bestScore = kvp.Value;
                bestOption = kvp.Key;
            }
        }

        if (bestOption == null)
        {
            return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.RankedChoice));
        }

        int winnerVoteCount = voteCounts[bestOption];
        double avgWinnerConfidence = confidenceByOption[bestOption] / winnerVoteCount;

        return new ConsensusResult(
            WinningOption: bestOption,
            AggregateConfidence: avgWinnerConfidence,
            AllVotes: votes,
            VoteCounts: voteCounts,
            ConfidenceByOption: confidenceByOption,
            HasConsensus: true,
            Protocol: nameof(ConsensusStrategy.RankedChoice));
    }

    /// <summary>
    /// Evaluates votes using a threshold-based approach.
    /// </summary>
    private ConsensusResult EvaluateThresholdBased(IReadOnlyList<AgentVote> votes, double threshold, string protocolName)
    {
        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        int totalVotes = votes.Count;
        string? winningOption = null;
        int maxVotes = 0;

        foreach (KeyValuePair<string, int> kvp in voteCounts)
        {
            if (kvp.Value > maxVotes)
            {
                maxVotes = kvp.Value;
                winningOption = kvp.Key;
            }
        }

        if (winningOption == null)
        {
            return ConsensusResult.NoConsensus(votes, protocolName);
        }

        double ratio = (double)maxVotes / totalVotes;

        if (ratio > threshold)
        {
            double avgConfidence = confidenceByOption[winningOption] / maxVotes;

            return new ConsensusResult(
                WinningOption: winningOption,
                AggregateConfidence: avgConfidence,
                AllVotes: votes,
                VoteCounts: voteCounts,
                ConfidenceByOption: confidenceByOption,
                HasConsensus: true,
                Protocol: protocolName);
        }

        return ConsensusResult.NoConsensus(votes, protocolName);
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
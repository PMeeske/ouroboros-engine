// <copyright file="CouncilDecision.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Represents the outcome of a council debate including votes, transcript, and minority opinions.
/// </summary>
/// <param name="Conclusion">The final synthesized conclusion from the debate.</param>
/// <param name="Votes">Dictionary mapping agent names to their votes.</param>
/// <param name="Transcript">List of debate rounds capturing the full discussion.</param>
/// <param name="Confidence">Confidence score for the decision (0.0 to 1.0).</param>
/// <param name="MinorityOpinions">List of dissenting opinions that were not adopted.</param>
public sealed record CouncilDecision(
    string Conclusion,
    IReadOnlyDictionary<string, AgentVote> Votes,
    IReadOnlyList<DebateRound> Transcript,
    double Confidence,
    IReadOnlyList<MinorityOpinion> MinorityOpinions)
{
    /// <summary>
    /// Gets whether the council reached consensus (all votes agree).
    /// </summary>
    public bool IsConsensus => Votes.Values.All(v => v.Position == Votes.Values.First().Position);

    /// <summary>
    /// Gets the majority position if one exists.
    /// </summary>
    public string? MajorityPosition
    {
        get
        {
            var grouped = Votes.Values.GroupBy(v => v.Position)
                .OrderByDescending(g => g.Sum(v => v.Weight))
                .FirstOrDefault();
            return grouped?.Key;
        }
    }

    /// <summary>
    /// Creates an empty decision representing a failed deliberation.
    /// </summary>
    /// <param name="reason">The reason for the failed deliberation.</param>
    /// <returns>A CouncilDecision indicating failure.</returns>
    public static CouncilDecision Failed(string reason) =>
        new(
            Conclusion: $"Deliberation failed: {reason}",
            Votes: new Dictionary<string, AgentVote>(),
            Transcript: [],
            Confidence: 0.0,
            MinorityOpinions: []);
}
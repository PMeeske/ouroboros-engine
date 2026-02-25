namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Defines the contract for consensus protocol implementations.
/// </summary>
public interface IConsensusProtocol
{
    /// <summary>
    /// Gets the consensus strategy used by this protocol.
    /// </summary>
    ConsensusStrategy Strategy { get; }

    /// <summary>
    /// Evaluates the given votes and determines if consensus is reached.
    /// </summary>
    /// <param name="votes">The votes to evaluate.</param>
    /// <returns>The consensus result.</returns>
    ConsensusResult Evaluate(IReadOnlyList<AgentVote> votes);

    /// <summary>
    /// Determines if the votes meet a specified threshold.
    /// </summary>
    /// <param name="votes">The votes to evaluate.</param>
    /// <param name="threshold">The threshold to meet (0.0 to 1.0).</param>
    /// <returns>True if threshold is met; otherwise, false.</returns>
    bool MeetsThreshold(IReadOnlyList<AgentVote> votes, double threshold);
}
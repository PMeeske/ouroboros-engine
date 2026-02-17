namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Defines the available consensus strategies.
/// </summary>
public enum ConsensusStrategy
{
    /// <summary>
    /// Simple majority wins (greater than 50%).
    /// </summary>
    Majority,

    /// <summary>
    /// Super majority required (greater than 66%).
    /// </summary>
    SuperMajority,

    /// <summary>
    /// Unanimous agreement required (100%).
    /// </summary>
    Unanimous,

    /// <summary>
    /// Votes weighted by confidence level.
    /// </summary>
    WeightedByConfidence,

    /// <summary>
    /// Single highest confidence vote wins.
    /// </summary>
    HighestConfidence,

    /// <summary>
    /// Ranked choice voting with elimination rounds.
    /// </summary>
    RankedChoice,
}
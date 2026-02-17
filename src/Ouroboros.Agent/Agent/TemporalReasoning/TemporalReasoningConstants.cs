namespace Ouroboros.Agent.TemporalReasoning;

/// <summary>
/// Configuration constants for temporal reasoning.
/// </summary>
internal static class TemporalReasoningConstants
{
    /// <summary>
    /// Maximum number of events to consider when computing relations in timeline construction.
    /// </summary>
    public const int MaxRelationLookahead = 5;

    /// <summary>
    /// Maximum time window (in minutes) for considering causal relationships.
    /// </summary>
    public const double MaxCausalityWindowMinutes = 60.0;
}
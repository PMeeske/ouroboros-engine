namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents an experience that the Ouroboros system has learned from.
/// </summary>
/// <param name="Id">Unique identifier for the experience.</param>
/// <param name="Goal">The goal that was pursued.</param>
/// <param name="Success">Whether the experience was successful.</param>
/// <param name="QualityScore">Quality score from 0.0 to 1.0.</param>
/// <param name="Insights">Key insights learned from this experience.</param>
/// <param name="Timestamp">When this experience occurred.</param>
public sealed record OuroborosExperience(
    Guid Id,
    string Goal,
    bool Success,
    double QualityScore,
    IReadOnlyList<string> Insights,
    DateTime Timestamp);
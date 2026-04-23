// ==========================================================
// Global Workspace Theory — Competition-for-Broadcast
// Plan 1: SalienceScorer
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Calculates salience scores for workspace candidates.
/// </summary>
public sealed class SalienceScorer
{
    /// <summary>
    /// Weight applied to urgency in salience calculation.
    /// </summary>
    public double UrgencyWeight { get; init; } = 0.3;

    /// <summary>
    /// Weight applied to novelty in salience calculation.
    /// </summary>
    public double NoveltyWeight { get; init; } = 0.25;

    /// <summary>
    /// Weight applied to relevance in salience calculation.
    /// </summary>
    public double RelevanceWeight { get; init; } = 0.25;

    /// <summary>
    /// Weight applied to confidence in salience calculation.
    /// </summary>
    public double ConfidenceWeight { get; init; } = 0.2;

    /// <summary>
    /// Calculates the salience score for a candidate.
    /// </summary>
    /// <param name="candidate">The candidate to score</param>
    /// <returns>Salience value in range [0, 1]</returns>
    public double CalculateSalience(Candidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return Math.Clamp(
            (candidate.Urgency * UrgencyWeight)
            + (candidate.Novelty * NoveltyWeight)
            + (candidate.Relevance * RelevanceWeight)
            + (candidate.Confidence * ConfidenceWeight),
            0.0,
            1.0);
    }
}

// ==========================================================
// Global Workspace Theory — Entropy-Based Intrinsic Drive
// Plan 6: DriveInfluencer adjusts candidate salience before competition
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Wraps the competition engine and intrinsic drive to influence
/// candidate salience before competition based on workspace entropy.
/// </summary>
public sealed class DriveInfluencer
{
    private readonly SalienceScorer _scorer;
    private readonly EntropyCalculator _entropyCalculator;
    private readonly IntrinsicDrive _intrinsicDrive;

    /// <summary>
    /// Creates a new drive influencer.
    /// </summary>
    /// <param name="scorer">Salience scorer</param>
    /// <param name="entropyCalculator">Entropy calculator</param>
    /// <param name="intrinsicDrive">Intrinsic drive configuration</param>
    public DriveInfluencer(
        SalienceScorer scorer,
        EntropyCalculator entropyCalculator,
        IntrinsicDrive intrinsicDrive)
    {
        _scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));
        _entropyCalculator = entropyCalculator ?? throw new ArgumentNullException(nameof(entropyCalculator));
        _intrinsicDrive = intrinsicDrive ?? throw new ArgumentNullException(nameof(intrinsicDrive));
    }

    /// <summary>
    /// Calculates adjusted salience scores for candidates based on current drive state.
    /// </summary>
    /// <param name="candidates">Candidates to score</param>
    /// <param name="currentChunks">Current workspace chunks for entropy calculation</param>
    /// <returns>Scored candidates with drive-adjusted salience</returns>
    public IReadOnlyList<ScoredCandidate> Influence(
        IEnumerable<Candidate> candidates,
        IEnumerable<WorkspaceChunk>? currentChunks = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        double entropy = _entropyCalculator.CalculateEntropy(currentChunks ?? Array.Empty<WorkspaceChunk>());
        DriveState state = _intrinsicDrive.EvaluateState(entropy);

        return candidates
            .Select(c =>
            {
                double baseSalience = _scorer.CalculateSalience(c);
                double adjusted = _intrinsicDrive.AdjustSalience(c, baseSalience, state);
                return new ScoredCandidate(c, Math.Clamp(adjusted, 0.0, 1.0));
            })
            .OrderByDescending(sc => sc.Salience)
            .ToList();
    }
}

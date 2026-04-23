// ==========================================================
// Global Workspace Theory — Competition-for-Broadcast
// Plan 1: CompetitionEngine
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Selects top-N candidates by salience from competing producers.
/// </summary>
public sealed class CompetitionEngine
{
    private readonly SalienceScorer _scorer;

    /// <summary>
    /// Creates a new competition engine.
    /// </summary>
    /// <param name="scorer">Optional custom salience scorer; defaults to standard weights</param>
    public CompetitionEngine(SalienceScorer? scorer = null)
    {
        _scorer = scorer ?? new SalienceScorer();
    }

    /// <summary>
    /// Competes all candidates and returns the top N winners by salience.
    /// </summary>
    /// <param name="candidates">All candidates competing for workspace access</param>
    /// <param name="topN">Number of winners to select</param>
    /// <returns>Winners ordered by descending salience</returns>
    public IReadOnlyList<ScoredCandidate> Compete(IEnumerable<Candidate> candidates, int topN)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topN);

        return candidates
            .Select(c => new ScoredCandidate(c, _scorer.CalculateSalience(c)))
            .OrderByDescending(sc => sc.Salience)
            .Take(topN)
            .ToList();
    }

    /// <summary>
    /// Gathers candidates from all registered producers and competes them.
    /// </summary>
    /// <param name="producers">Subsystems producing candidates</param>
    /// <param name="topN">Number of winners to select</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Winners ordered by descending salience</returns>
    public async Task<IReadOnlyList<ScoredCandidate>> CompeteAsync(
        IEnumerable<ICandidateProducer> producers,
        int topN,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(producers);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topN);

        var allCandidates = new List<Candidate>();

        foreach (ICandidateProducer producer in producers)
        {
            IReadOnlyList<Candidate> produced = await producer.ProduceCandidatesAsync(ct).ConfigureAwait(false);
            allCandidates.AddRange(produced);
        }

        return Compete(allCandidates, topN);
    }
}

/// <summary>
/// A candidate with its computed salience score.
/// </summary>
public sealed record ScoredCandidate(Candidate Candidate, double Salience);

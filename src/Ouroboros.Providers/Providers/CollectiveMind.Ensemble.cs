using System.Text;

namespace Ouroboros.Providers;

public sealed partial class CollectiveMind
{
    /// <summary>
    /// Ensemble mode: Query multiple providers and elect the best response via master orchestration.
    /// </summary>
    private async Task<ThinkingResponse> ThinkWithEnsemble(string prompt, CancellationToken ct)
    {
        _thoughtStream.OnNext("🎭 Ensemble mode: gathering perspectives from multiple pathways...");

        // Exclude master from worker pathways to avoid self-evaluation
        var workerPathways = _pathways
            .Where(p => p.IsHealthy && p != _masterPathway)
            .Take(5)
            .ToList();

        if (workerPathways.Count == 0)
        {
            // Fall back to all healthy pathways including master
            workerPathways = _pathways.Where(p => p.IsHealthy).Take(3).ToList();
        }

        if (workerPathways.Count == 0)
            throw new InvalidOperationException("No healthy neural pathways available");

        // Query all worker pathways in parallel
        var tasks = workerPathways.Select(async pathway =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await pathway.CircuitBreaker.ExecuteAsync(async () =>
                    await QueryPathway(pathway, prompt, ct));
                sw.Stop();
                pathway.RecordActivation(sw.Elapsed);

                return ResponseCandidate<ThinkingResponse>.Create(result, pathway.Name, sw.Elapsed);
            }
            catch
            {
                pathway.RecordInhibition();
                return ResponseCandidate<ThinkingResponse>.Invalid(pathway.Name);
            }
        });

        var candidates = (await Task.WhenAll(tasks)).ToList();
        var validCandidates = candidates.Where(c => c.IsValid && !string.IsNullOrEmpty(c.Value.Content)).ToList();

        if (validCandidates.Count == 0)
            throw new InvalidOperationException("No pathways returned valid responses");

        // Single valid response - return directly
        if (validCandidates.Count == 1)
        {
            var solo = validCandidates[0];
            var pathway = _pathways.First(p => p.Name == solo.Source);
            AggregateCosts(pathway);
            return solo.Value;
        }

        // Multiple responses - run election with master orchestration
        _thoughtStream.OnNext($"🗳️ Running election with {validCandidates.Count} candidates...");

        if (_election != null)
        {
            var electionResult = await _election.RunElectionAsync(validCandidates, prompt, ct);

            // Aggregate costs from all queried pathways
            foreach (var c in validCandidates)
            {
                var pathway = _pathways.FirstOrDefault(p => p.Name == c.Source);
                if (pathway != null) AggregateCosts(pathway);
            }

            // Build thinking trace with election details
            var synthesis = new StringBuilder();
            synthesis.AppendLine($"🗳️ Election Results ({electionResult.Strategy}):");
            synthesis.AppendLine($"   {electionResult.Rationale}");
            synthesis.AppendLine();
            foreach (var (source, votes) in electionResult.Votes.OrderByDescending(kv => kv.Value))
            {
                string marker = source == electionResult.Winner.Source ? "→" : " ";
                synthesis.AppendLine($"   {marker} {source}: {votes:F3}");
            }

            _thoughtStream.OnNext($"👑 Winner: {electionResult.Winner.Source}");

            return new ThinkingResponse(synthesis.ToString(), electionResult.Winner.Value.Content);
        }

        // Fallback: select by pathway weight if no election system
        var best = validCandidates
            .Select(c => (Candidate: c, Pathway: _pathways.First(p => p.Name == c.Source)))
            .OrderByDescending(x => x.Pathway.Weight * x.Pathway.ActivationRate)
            .First();

        foreach (var c in validCandidates)
        {
            var pathway = _pathways.FirstOrDefault(p => p.Name == c.Source);
            if (pathway != null) AggregateCosts(pathway);
        }

        return best.Candidate.Value;
    }
}

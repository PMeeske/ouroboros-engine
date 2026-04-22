// ==========================================================
// Global Workspace Theory — Entropy-Based Intrinsic Drive
// Plan 6: EntropyCalculator
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Measures entropy from the distribution of workspace chunks by source subsystem.
/// </summary>
public sealed class EntropyCalculator
{
    /// <summary>
    /// Calculates Shannon entropy from workspace chunk distribution.
    /// </summary>
    /// <param name="chunks">Current workspace chunks</param>
    /// <returns>Entropy in range [0, ~log(N)]; normalized to [0, 1] when possible</returns>
    public double CalculateEntropy(IEnumerable<WorkspaceChunk> chunks)
    {
        List<WorkspaceChunk> list = chunks?.ToList() ?? new List<WorkspaceChunk>();

        if (list.Count == 0)
        {
            return 0.0;
        }

        Dictionary<string, int> counts = list
            .GroupBy(c => c.Candidate.SourceSubsystem)
            .ToDictionary(g => g.Key, g => g.Count());

        int total = list.Count;
        double entropy = 0.0;

        foreach (int count in counts.Values)
        {
            double p = count / (double)total;
            if (p > 0)
            {
                entropy -= p * Math.Log(p);
            }
        }

        // Normalize to [0, 1] using max possible entropy for N distinct sources
        double maxEntropy = Math.Log(counts.Count);
        if (maxEntropy > 0)
        {
            entropy = Math.Min(1.0, entropy / maxEntropy);
        }

        return entropy;
    }
}

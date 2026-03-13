using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Ouroboros.Providers;

/// <summary>
/// Partial class containing election algorithms, performance tracking, and heuristic scoring.
/// </summary>
public sealed partial class MasterModelElection
{
    // ===============================================================================
    // ELECTION ALGORITHMS (Pure Functions)
    // ===============================================================================

    private static (ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)
        ElectByMajority(List<ResponseCandidate<ThinkingResponse>> candidates)
    {
        var ranked = candidates.OrderByDescending(c => c.Score).ToList();
        var winner = ranked[0];
        var votes = candidates.ToDictionary(c => c.Source, c => c.Score);
        return (winner, votes, $"Highest score: {winner.Score:F3}");
    }

    private (ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)
        ElectByWeightedMajority(List<ResponseCandidate<ThinkingResponse>> candidates)
    {
        var weighted = candidates.Select(c =>
        {
            var perf = _performanceHistory.GetValueOrDefault(c.Source);
            double reliability = perf?.ReliabilityScore ?? 0.5;
            double weightedScore = c.Score * (0.5 + reliability * 0.5);
            return (Candidate: c, WeightedScore: weightedScore);
        }).ToList();

        var rankedWeighted = weighted.OrderByDescending(w => w.WeightedScore).ToList();
        var winner = rankedWeighted[0];
        var votes = weighted.ToDictionary(w => w.Candidate.Source, w => w.WeightedScore);
        return (winner.Candidate, votes, $"Weighted score: {winner.WeightedScore:F3} (reliability factored)");
    }

    private static (ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)
        ElectByBordaCount(List<ResponseCandidate<ThinkingResponse>> candidates)
    {
        int n = candidates.Count;
        var ranked = candidates.OrderByDescending(c => c.Score).ToList();
        var votes = new Dictionary<string, double>();

        for (int i = 0; i < ranked.Count; i++)
        {
            votes[ranked[i].Source] = n - i; // Borda points: n for 1st, n-1 for 2nd, etc.
        }

        var winner = ranked[0];
        return (winner, votes, $"Borda count winner with {n} points");
    }

    private static async Task<(ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)>
        ElectByCondorcetAsync(List<ResponseCandidate<ThinkingResponse>> candidates, string prompt, CancellationToken ct)
    {
        // Simplified Condorcet: use scores for pairwise comparison
        var wins = candidates.ToDictionary(c => c.Source, _ => 0);

        for (int i = 0; i < candidates.Count; i++)
        {
            for (int j = i + 1; j < candidates.Count; j++)
            {
                if (candidates[i].Score > candidates[j].Score)
                    wins[candidates[i].Source]++;
                else if (candidates[j].Score > candidates[i].Score)
                    wins[candidates[j].Source]++;
            }
        }

        var rankedWins = wins.OrderByDescending(kv => kv.Value).ToList();
        var winnerSource = rankedWins[0].Key;
        var winner = candidates.Find(c => c.Source == winnerSource)!;
        var votes = wins.ToDictionary(kv => kv.Key, kv => (double)kv.Value);

        return (winner, votes, $"Condorcet winner with {wins[winnerSource]} pairwise wins");
    }

    private static (ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)
        ElectByInstantRunoff(List<ResponseCandidate<ThinkingResponse>> candidates)
    {
        var remaining = candidates.ToList();
        var votes = candidates.ToDictionary(c => c.Source, c => c.Score);
        int rounds = 0;

        while (remaining.Count > 1)
        {
            rounds++;
            var lowest = remaining.OrderBy(c => c.Score).ToList()[0];
            remaining.Remove(lowest);

            if (remaining.Count > 0)
            {
                // Redistribute (simplified: just remove)
                votes[lowest.Source] = -rounds; // Negative indicates elimination round
            }
        }

        var winner = remaining[0];
        return (winner, votes, $"IRV winner after {rounds} elimination rounds");
    }

    private static (ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)
        ElectByApproval(List<ResponseCandidate<ThinkingResponse>> candidates, double threshold)
    {
        var approved = candidates.Where(c => c.Score >= threshold).ToList();
        var votes = candidates.ToDictionary(c => c.Source, c => c.Score >= threshold ? 1.0 : 0.0);

        if (approved.Count == 0)
        {
            // Lower threshold if no approvals
            approved = candidates.OrderByDescending(c => c.Score).Take(1).ToList();
        }

        var rankedApproved = approved.OrderByDescending(c => c.Score).ToList();
        var winner = rankedApproved[0];
        return (winner, votes, $"Approval voting: {approved.Count} candidates above threshold {threshold}");
    }

    private async Task<(ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)>
        ElectByMasterDecisionAsync(List<ResponseCandidate<ThinkingResponse>> candidates, string prompt, CancellationToken ct)
    {
        if (_masterPathway?.IsHealthy != true)
        {
            return ElectByWeightedMajority(candidates);
        }

        try
        {
            var decisionPrompt = new StringBuilder()
                .AppendLine("Select the BEST response. Reply with ONLY the response number (1, 2, 3, etc.).")
                .AppendLine($"Original: {prompt.Substring(0, Math.Min(150, prompt.Length))}...")
                .AppendLine();

            for (int i = 0; i < candidates.Count; i++)
            {
                var preview = candidates[i].Value.Content;
                if (preview.Length > 200) preview = preview.Substring(0, 200) + "...";
                decisionPrompt.AppendLine($"{i + 1}. [{candidates[i].Source}]: {preview}");
            }

            var decision = await _masterPathway.CircuitBreaker.ExecuteAsync(async () =>
                await _masterPathway.Model.GenerateTextAsync(decisionPrompt.ToString(), ct).ConfigureAwait(false)).ConfigureAwait(false);

            // Parse the selected number
            var match = DigitRegex().Match(decision);
            if (match.Success && int.TryParse(match.Value, out int selected) &&
                selected >= 1 && selected <= candidates.Count)
            {
                var winner = candidates[selected - 1];
                var votes = candidates.ToDictionary(c => c.Source, c => c == winner ? 1.0 : 0.0);
                return (winner, votes, $"Master model selected response #{selected}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fall back to weighted majority
        }

        return ElectByWeightedMajority(candidates);
    }

    // ===============================================================================
    // PERFORMANCE TRACKING & OPTIMIZATION
    // ===============================================================================

    private void UpdatePerformanceHistory(ResponseCandidate<ThinkingResponse> candidate, bool wasWinner)
    {
        _performanceHistory.AddOrUpdate(
            candidate.Source,
            _ => new ModelPerformance
            {
                ModelName = candidate.Source,
                TotalElections = 1,
                Wins = wasWinner ? 1 : 0,
                AverageScore = candidate.Score,
                AverageLatency = candidate.Latency,
                LastUsed = DateTime.UtcNow
            },
            (_, perf) =>
            {
                perf.TotalElections++;
                if (wasWinner) perf.Wins++;
                perf.AverageScore = perf.AverageScore * 0.9 + candidate.Score * 0.1;
                perf.AverageLatency = TimeSpan.FromMilliseconds(
                    perf.AverageLatency.TotalMilliseconds * 0.9 + candidate.Latency.TotalMilliseconds * 0.1);
                perf.LastUsed = DateTime.UtcNow;
                return perf;
            });
    }

    /// <summary>
    /// Gets optimization suggestions based on performance history.
    /// </summary>
    public IReadOnlyList<OptimizationSuggestion> GetOptimizationSuggestions()
    {
        var suggestions = new List<OptimizationSuggestion>();

        foreach (var (source, perf) in _performanceHistory)
        {
            if (perf.WinRate < 0.2 && perf.TotalElections > 5)
            {
                suggestions.Add(new OptimizationSuggestion(
                    source,
                    OptimizationType.ConsiderRemoving,
                    $"Low win rate ({perf.WinRate:P0}) over {perf.TotalElections} elections",
                    Priority: 2));
            }

            if (perf.AverageLatency.TotalSeconds > 10 && perf.WinRate < 0.5)
            {
                suggestions.Add(new OptimizationSuggestion(
                    source,
                    OptimizationType.ReduceUsage,
                    $"High latency ({perf.AverageLatency.TotalSeconds:F1}s) with moderate win rate",
                    Priority: 1));
            }

            if (perf.WinRate > 0.7 && perf.TotalElections > 10)
            {
                suggestions.Add(new OptimizationSuggestion(
                    source,
                    OptimizationType.IncreasePriority,
                    $"High performer ({perf.WinRate:P0} win rate)",
                    Priority: 3));
            }
        }

        return suggestions.OrderByDescending(s => s.Priority).ToList();
    }

    // ===============================================================================
    // HEURISTIC SCORING FUNCTIONS (Pure)
    // ===============================================================================

    private static double CalculateRelevance(string response, string prompt)
    {
        if (string.IsNullOrEmpty(response)) return 0;

        var promptWords = ExtractWords(prompt);
        var responseWords = ExtractWords(response);

        if (promptWords.Count == 0) return 0.5;

        int overlap = promptWords.Intersect(responseWords).Count();
        return Math.Min(1.0, (double)overlap / promptWords.Count);
    }

    private static double CalculateCoherence(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        // Heuristics: sentence count, average length, punctuation
        var sentences = SentenceSplitRegex().Split(text).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (sentences.Count == 0) return 0.3;

        double avgLength = sentences.Average(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

        // Ideal sentence length: 10-25 words
        double lengthScore = avgLength switch
        {
            < 5 => 0.5,
            < 10 => 0.7,
            <= 25 => 1.0,
            <= 40 => 0.8,
            _ => 0.6
        };

        // More sentences = more coherent structure (up to a point)
        double structureScore = Math.Min(1.0, sentences.Count / 5.0);

        return (lengthScore * 0.6 + structureScore * 0.4);
    }

    private static double CalculateCompleteness(string response, string prompt)
    {
        if (string.IsNullOrEmpty(response)) return 0;

        // Heuristic: response length relative to prompt complexity
        int promptComplexity = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        int responseLength = response.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        // Ideal: response 2-5x longer than prompt
        double ratio = (double)responseLength / Math.Max(1, promptComplexity);

        return ratio switch
        {
            < 0.5 => 0.3,
            < 1 => 0.5,
            < 2 => 0.7,
            <= 5 => 1.0,
            <= 10 => 0.9,
            _ => 0.7
        };
    }

    private static HashSet<string> ExtractWords(string text)
    {
        return WordsRegex().Matches(text.ToLowerInvariant())
            .Cast<Match>()
            .Select(m => m.Value)
            .ToHashSet();
    }
}

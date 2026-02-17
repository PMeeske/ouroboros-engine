using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Ouroboros.Providers;

/// <summary>
/// Master model election system for dynamic model optimization.
/// Uses a designated master model to evaluate and select the best response.
/// Clean, functional design with monadic composition.
/// </summary>
public sealed class MasterModelElection : IDisposable
{
    private readonly NeuralPathway? _masterPathway;
    private readonly Subject<ElectionEvent> _electionEvents = new();
    private readonly ConcurrentDictionary<string, ModelPerformance> _performanceHistory = new();
    private readonly EvaluationCriteria _criteria;
    private ElectionStrategy _strategy;

    /// <summary>Observable stream of election events.</summary>
    public IObservable<ElectionEvent> ElectionEvents => _electionEvents.AsObservable();

    /// <summary>Current election strategy.</summary>
    public ElectionStrategy Strategy
    {
        get => _strategy;
        set => _strategy = value;
    }

    /// <summary>Performance history for all models.</summary>
    public IReadOnlyDictionary<string, ModelPerformance> PerformanceHistory => _performanceHistory;

    public MasterModelElection(
        NeuralPathway? masterPathway = null,
        ElectionStrategy strategy = ElectionStrategy.WeightedMajority,
        EvaluationCriteria? criteria = null)
    {
        _masterPathway = masterPathway;
        _strategy = strategy;
        _criteria = criteria ?? EvaluationCriteria.Default;
    }

    /// <summary>
    /// Runs an election to select the best response from candidates.
    /// Pure function that returns the election result.
    /// </summary>
    public async Task<ElectionResult<ThinkingResponse>> RunElectionAsync(
        IReadOnlyList<ResponseCandidate<ThinkingResponse>> candidates,
        string originalPrompt,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0)
            throw new InvalidOperationException("No candidates for election");

        var validCandidates = candidates.Where(c => c.IsValid).ToList();
        if (validCandidates.Count == 0)
            throw new InvalidOperationException("No valid candidates for election");

        // Single candidate = automatic winner
        if (validCandidates.Count == 1)
        {
            var solo = validCandidates[0].WithScore(1.0);
            return new ElectionResult<ThinkingResponse>(
                solo, validCandidates, _strategy,
                "Single candidate - automatic selection",
                new Dictionary<string, double> { [solo.Source] = 1.0 });
        }

        // Score all candidates
        var scored = await ScoreCandidatesAsync(validCandidates, originalPrompt, ct);

        // Apply election strategy
        var (winner, votes, rationale) = _strategy switch
        {
            ElectionStrategy.Majority => ElectByMajority(scored),
            ElectionStrategy.WeightedMajority => ElectByWeightedMajority(scored),
            ElectionStrategy.BordaCount => ElectByBordaCount(scored),
            ElectionStrategy.Condorcet => await ElectByCondorcetAsync(scored, originalPrompt, ct),
            ElectionStrategy.InstantRunoff => ElectByInstantRunoff(scored),
            ElectionStrategy.ApprovalVoting => ElectByApproval(scored, threshold: 0.6),
            ElectionStrategy.MasterDecision => await ElectByMasterDecisionAsync(scored, originalPrompt, ct),
            _ => ElectByMajority(scored)
        };

        // Update performance history
        foreach (var candidate in scored)
        {
            UpdatePerformanceHistory(candidate, candidate == winner);
        }

        _electionEvents.OnNext(new ElectionEvent(
            ElectionEventType.ElectionComplete,
            $"Winner: {winner.Source} via {_strategy}",
            DateTime.UtcNow,
            winner.Source,
            votes));

        return new ElectionResult<ThinkingResponse>(winner, scored, _strategy, rationale, votes);
    }

    /// <summary>
    /// Scores candidates based on evaluation criteria.
    /// </summary>
    private async Task<List<ResponseCandidate<ThinkingResponse>>> ScoreCandidatesAsync(
        IReadOnlyList<ResponseCandidate<ThinkingResponse>> candidates,
        string originalPrompt,
        CancellationToken ct)
    {
        var scored = new List<ResponseCandidate<ThinkingResponse>>();

        foreach (var candidate in candidates)
        {
            var metrics = new Dictionary<string, double>();

            // Relevance: simple heuristic based on prompt term overlap
            metrics["relevance"] = CalculateRelevance(candidate.Value.Content, originalPrompt);

            // Coherence: sentence structure and flow
            metrics["coherence"] = CalculateCoherence(candidate.Value.Content);

            // Completeness: response length relative to prompt complexity
            metrics["completeness"] = CalculateCompleteness(candidate.Value.Content, originalPrompt);

            // Latency: normalized inverse (faster = higher score)
            metrics["latency"] = Math.Max(0, 1 - candidate.Latency.TotalSeconds / 30);

            // Cost: from performance history
            var perf = _performanceHistory.GetValueOrDefault(candidate.Source);
            metrics["cost"] = perf?.AverageCost > 0 ? Math.Max(0, 1 - perf.AverageCost / 0.01) : 0.5;

            // Compute weighted score
            double score =
                metrics["relevance"] * _criteria.RelevanceWeight +
                metrics["coherence"] * _criteria.CoherenceWeight +
                metrics["completeness"] * _criteria.CompletenessWeight +
                metrics["latency"] * _criteria.LatencyWeight +
                metrics["cost"] * _criteria.CostWeight;

            scored.Add(candidate.WithScore(score).WithMetrics(metrics));
        }

        // If master pathway available, get its evaluation
        if (_masterPathway?.IsHealthy == true)
        {
            scored = await EnhanceWithMasterEvaluationAsync(scored, originalPrompt, ct);
        }

        return scored;
    }

    /// <summary>
    /// Uses master model to enhance scoring with semantic evaluation.
    /// </summary>
    private async Task<List<ResponseCandidate<ThinkingResponse>>> EnhanceWithMasterEvaluationAsync(
        List<ResponseCandidate<ThinkingResponse>> candidates,
        string originalPrompt,
        CancellationToken ct)
    {
        try
        {
            var evaluationPrompt = BuildEvaluationPrompt(candidates, originalPrompt);

            var masterResponse = await _masterPathway!.CircuitBreaker.ExecuteAsync(async () =>
                await _masterPathway.Model.GenerateTextAsync(evaluationPrompt, ct));

            var masterScores = ParseMasterScores(masterResponse, candidates.Count);

            for (int i = 0; i < candidates.Count && i < masterScores.Count; i++)
            {
                // Blend heuristic score with master evaluation
                double blendedScore = candidates[i].Score * 0.4 + masterScores[i] * 0.6;
                candidates[i] = candidates[i].WithScore(blendedScore);
            }

            _electionEvents.OnNext(new ElectionEvent(
                ElectionEventType.MasterEvaluation,
                "Master model evaluation complete",
                DateTime.UtcNow));
        }
        catch
        {
            // Master evaluation failed, use heuristic scores only
            _electionEvents.OnNext(new ElectionEvent(
                ElectionEventType.MasterEvaluationFailed,
                "Falling back to heuristic scoring",
                DateTime.UtcNow));
        }

        return candidates;
    }

    private static string BuildEvaluationPrompt(
        IReadOnlyList<ResponseCandidate<ThinkingResponse>> candidates,
        string originalPrompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are evaluating multiple AI responses to select the best one.");
        sb.AppendLine("Rate each response 0.0-1.0 based on relevance, coherence, and completeness.");
        sb.AppendLine("Return ONLY a JSON array of scores, e.g.: [0.8, 0.6, 0.9]");
        sb.AppendLine();
        sb.AppendLine($"Original prompt: {originalPrompt.Substring(0, Math.Min(200, originalPrompt.Length))}...");
        sb.AppendLine();

        for (int i = 0; i < candidates.Count; i++)
        {
            var preview = candidates[i].Value.Content;
            if (preview.Length > 300) preview = preview.Substring(0, 300) + "...";
            sb.AppendLine($"Response {i + 1} ({candidates[i].Source}):");
            sb.AppendLine(preview);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static List<double> ParseMasterScores(string response, int expectedCount)
    {
        var scores = new List<double>();
        var matches = Regex.Matches(response, @"0?\.\d+|1\.0|0|1");
        foreach (Match m in matches)
        {
            if (double.TryParse(m.Value, out double score))
            {
                scores.Add(Math.Clamp(score, 0, 1));
                if (scores.Count >= expectedCount) break;
            }
        }

        // Pad with defaults if needed
        while (scores.Count < expectedCount)
            scores.Add(0.5);

        return scores;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ELECTION ALGORITHMS (Pure Functions)
    // ═══════════════════════════════════════════════════════════════════════════

    private static (ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)
        ElectByMajority(List<ResponseCandidate<ThinkingResponse>> candidates)
    {
        var winner = candidates.OrderByDescending(c => c.Score).First();
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

        var winner = weighted.OrderByDescending(w => w.WeightedScore).First();
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

        var winner = ranked.First();
        return (winner, votes, $"Borda count winner with {n} points");
    }

    private async Task<(ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)>
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

        var winnerSource = wins.OrderByDescending(kv => kv.Value).First().Key;
        var winner = candidates.First(c => c.Source == winnerSource);
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
            var lowest = remaining.OrderBy(c => c.Score).First();
            remaining.Remove(lowest);

            if (remaining.Count > 0)
            {
                // Redistribute (simplified: just remove)
                votes[lowest.Source] = -rounds; // Negative indicates elimination round
            }
        }

        var winner = remaining.First();
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

        var winner = approved.OrderByDescending(c => c.Score).First();
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
                await _masterPathway.Model.GenerateTextAsync(decisionPrompt.ToString(), ct));

            // Parse the selected number
            var match = Regex.Match(decision, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int selected) &&
                selected >= 1 && selected <= candidates.Count)
            {
                var winner = candidates[selected - 1];
                var votes = candidates.ToDictionary(c => c.Source, c => c == winner ? 1.0 : 0.0);
                return (winner, votes, $"Master model selected response #{selected}");
            }
        }
        catch
        {
            // Fall back to weighted majority
        }

        return ElectByWeightedMajority(candidates);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PERFORMANCE TRACKING & OPTIMIZATION
    // ═══════════════════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════════════════
    // HEURISTIC SCORING FUNCTIONS (Pure)
    // ═══════════════════════════════════════════════════════════════════════════

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
        var sentences = Regex.Split(text, @"[.!?]+").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
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
        return Regex.Matches(text.ToLowerInvariant(), @"\b[a-z]{3,}\b")
            .Cast<Match>()
            .Select(m => m.Value)
            .ToHashSet();
    }

    public void Dispose()
    {
        _electionEvents.OnCompleted();
        _electionEvents.Dispose();
    }
}
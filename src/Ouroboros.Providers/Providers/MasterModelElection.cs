using R3;

namespace Ouroboros.Providers;

/// <summary>
/// Master model election system for dynamic model optimization.
/// Uses a designated master model to evaluate and select the best response.
/// Clean, functional design with monadic composition.
/// </summary>
public sealed partial class MasterModelElection : IDisposable
{
    private readonly NeuralPathway? _masterPathway;
    private readonly Subject<ElectionEvent> _electionEvents = new();
    private readonly ConcurrentDictionary<string, ModelPerformance> _performanceHistory = new();
    private readonly EvaluationCriteria _criteria;
    private ElectionStrategy _strategy;

    /// <summary>Observable stream of election events.</summary>
    public Observable<ElectionEvent> ElectionEvents => _electionEvents;

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
        var scored = await ScoreCandidatesAsync(validCandidates, originalPrompt, ct).ConfigureAwait(false);

        // Apply election strategy
        var (winner, votes, rationale) = _strategy switch
        {
            ElectionStrategy.Majority => ElectByMajority(scored),
            ElectionStrategy.WeightedMajority => ElectByWeightedMajority(scored),
            ElectionStrategy.BordaCount => ElectByBordaCount(scored),
            ElectionStrategy.Condorcet => await ElectByCondorcetAsync(scored, originalPrompt, ct).ConfigureAwait(false),
            ElectionStrategy.InstantRunoff => ElectByInstantRunoff(scored),
            ElectionStrategy.ApprovalVoting => ElectByApproval(scored, threshold: 0.6),
            ElectionStrategy.MasterDecision => await ElectByMasterDecisionAsync(scored, originalPrompt, ct).ConfigureAwait(false),
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
            scored = await EnhanceWithMasterEvaluationAsync(scored, originalPrompt, ct).ConfigureAwait(false);
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
                await _masterPathway.Model.GenerateTextAsync(evaluationPrompt, ct).ConfigureAwait(false)).ConfigureAwait(false);

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
        catch (Exception ex) when (ex is not OperationCanceledException)
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
        var matches = ScoreRegex().Matches(response);
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

    public void Dispose()
    {
        _electionEvents.OnCompleted();
        _electionEvents.Dispose(false);
    }

    [GeneratedRegex(@"\b(0?\.\d+|1\.0)\b")]
    private static partial Regex ScoreRegex();

    [GeneratedRegex(@"[.!?]+")]
    private static partial Regex SentenceSplitRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitRegex();

    [GeneratedRegex(@"\b[a-z]{3,}\b")]
    private static partial Regex WordsRegex();
}
// Copyright (c) Ouroboros. All rights reserved.

// ==========================================================
// Counterfactual Engine
// Heuristic counterfactual reasoning with regret computation
// and contrastive explanation generation.
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI.SelfImprovement;

/// <summary>
/// Implements counterfactual reasoning using heuristic outcome quality estimation.
/// Supports alternative-action simulation, regret magnitude computation, and
/// contrastive explanation generation for outcome divergence.
/// </summary>
public sealed class CounterfactualEngine : ICounterfactualEngine
{
    private const int MaxRegretHistory = 200;
    private readonly ConcurrentQueue<RegretRecord> _regretHistory = new();

    /// <inheritdoc />
    public Task<Result<CounterfactualSimulation, string>> SimulateAlternativeAsync(
        string actualAction, string alternativeAction, string context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actualAction);
        ArgumentNullException.ThrowIfNull(alternativeAction);
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        double actualQuality = EstimateOutcomeQuality(actualAction, context);
        double alternativeQuality = EstimateOutcomeQuality(alternativeAction, context);
        double qualityDiff = Math.Round(alternativeQuality - actualQuality, 4);

        string predictedOutcome = qualityDiff > 0
            ? $"Alternative '{Truncate(alternativeAction)}' is predicted to yield a better outcome " +
              $"(+{qualityDiff:F2}) in context: {Truncate(context)}"
            : $"Alternative '{Truncate(alternativeAction)}' is predicted to yield a similar or worse outcome " +
              $"({qualityDiff:F2}) in context: {Truncate(context)}";

        var simulation = new CounterfactualSimulation(
            actualAction, alternativeAction, predictedOutcome, qualityDiff);

        return Task.FromResult(Result<CounterfactualSimulation, string>.Success(simulation));
    }

    /// <inheritdoc />
    public Task<Result<double, string>> ComputeRegretAsync(
        string actionTaken, string bestAlternative, string outcome,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actionTaken);
        ArgumentNullException.ThrowIfNull(bestAlternative);
        ArgumentNullException.ThrowIfNull(outcome);
        ct.ThrowIfCancellationRequested();

        double takenQuality = EstimateOutcomeQuality(actionTaken, outcome);
        double alternativeQuality = EstimateOutcomeQuality(bestAlternative, outcome);

        // Regret is clamped to [0, 1]; positive when the alternative was better
        double regret = Math.Clamp(alternativeQuality - takenQuality, 0.0, 1.0);
        regret = Math.Round(regret, 4);

        var record = new RegretRecord(actionTaken, bestAlternative, regret, DateTime.UtcNow);
        EnqueueRegret(record);

        return Task.FromResult(Result<double, string>.Success(regret));
    }

    /// <inheritdoc />
    public Task<Result<ContrastiveExplanation, string>> ExplainContrastivelyAsync(
        string actualOutcome, string expectedOutcome,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actualOutcome);
        ArgumentNullException.ThrowIfNull(expectedOutcome);
        ct.ThrowIfCancellationRequested();

        var actualWords = ExtractKeywords(actualOutcome);
        var expectedWords = ExtractKeywords(expectedOutcome);

        var onlyInActual = actualWords.Except(expectedWords, StringComparer.OrdinalIgnoreCase).ToList();
        var onlyInExpected = expectedWords.Except(actualWords, StringComparer.OrdinalIgnoreCase).ToList();

        var factors = new List<string>();
        if (onlyInActual.Count > 0)
            factors.Add($"Actual outcome introduced: {string.Join(", ", onlyInActual.Take(5))}");
        if (onlyInExpected.Count > 0)
            factors.Add($"Expected outcome included: {string.Join(", ", onlyInExpected.Take(5))}");
        if (factors.Count == 0)
            factors.Add("Outcomes share similar concepts but differ in emphasis or framing");

        string explanation = factors.Count > 0
            ? $"The actual outcome diverged from expectations because {string.Join("; and ", factors)}."
            : "No significant differentiating factors could be identified.";

        var result = new ContrastiveExplanation(
            actualOutcome, expectedOutcome, factors, explanation);

        return Task.FromResult(Result<ContrastiveExplanation, string>.Success(result));
    }

    /// <inheritdoc />
    public List<RegretRecord> GetRegretHistory(int count = 20)
    {
        return _regretHistory
            .Reverse()
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Returns the total number of regret records stored.
    /// </summary>
    public int TotalRegretRecords => _regretHistory.Count;

    /// <summary>
    /// Estimates outcome quality using a heuristic based on keyword specificity
    /// and overlap between the action and context. Returns a score in [0, 1].
    /// </summary>
    private static double EstimateOutcomeQuality(string action, string context)
    {
        var actionWords = ExtractKeywords(action);
        var contextWords = ExtractKeywords(context);

        if (actionWords.Count == 0 || contextWords.Count == 0)
            return 0.5;

        int overlap = actionWords.Intersect(contextWords, StringComparer.OrdinalIgnoreCase).Count();
        double relevance = (double)overlap / Math.Max(actionWords.Count, 1);

        // Specificity bonus: longer actions suggest more deliberate planning
        double specificity = Math.Min(actionWords.Count / 10.0, 0.3);

        return Math.Clamp(relevance * 0.7 + specificity + 0.1, 0.0, 1.0);
    }

    private static HashSet<string> ExtractKeywords(string text)
    {
        var words = text.Split(
            [' ', ',', '.', ';', ':', '-', '_', '/', '(', ')'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return [.. words.Where(w => w.Length > 2).Select(w => w.ToLowerInvariant())];
    }

    private static string Truncate(string text, int maxLen = 80)
    {
        return text.Length <= maxLen ? text : text[..maxLen] + "...";
    }

    private void EnqueueRegret(RegretRecord record)
    {
        _regretHistory.Enqueue(record);

        // Prune to bounded capacity
        while (_regretHistory.Count > MaxRegretHistory)
            _regretHistory.TryDequeue(out _);
    }
}

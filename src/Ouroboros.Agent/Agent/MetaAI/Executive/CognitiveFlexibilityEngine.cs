// ==========================================================
// Cognitive Flexibility Engine
// Strategy shifting and SCAMPER-based alternative generation
// ==========================================================

namespace Ouroboros.Agent.MetaAI.Executive;

/// <summary>
/// An alternative strategy with its generation method.
/// </summary>
/// <param name="Description">Description of the alternative strategy.</param>
/// <param name="ScamperOperator">The SCAMPER operator used to generate it.</param>
/// <param name="EstimatedEffectiveness">Estimated effectiveness (0–1).</param>
public sealed record AlternativeStrategy(
    string Description,
    string ScamperOperator,
    double EstimatedEffectiveness);

/// <summary>
/// Implements cognitive flexibility via strategy evaluation, shifting,
/// and SCAMPER-based alternative generation. Tracks shift success rates
/// and estimates task-switching costs based on domain overlap.
/// </summary>
public sealed class CognitiveFlexibilityEngine
{
    private readonly ConcurrentDictionary<string, List<bool>> _strategyOutcomes = new();
    private int _totalShifts;
    private int _successfulShifts;
    private readonly object _lock = new();

    private static readonly string[] ScamperOperators =
    [
        "Substitute", "Combine", "Adapt", "Modify",
        "Put to another use", "Eliminate", "Reverse"
    ];

    /// <summary>
    /// Evaluates whether the current strategy should be shifted based on recent outcomes.
    /// A shift is recommended after 3 or more consecutive failures.
    /// </summary>
    /// <param name="currentStrategy">Name or description of the current strategy.</param>
    /// <param name="recentOutcomes">Recent outcome results (true = success, false = failure).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="StrategyShiftResult"/> indicating whether to shift.</returns>
    public Task<StrategyShiftResult> EvaluateStrategyAsync(
        string currentStrategy,
        IReadOnlyList<bool> recentOutcomes,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(currentStrategy);
        ArgumentNullException.ThrowIfNull(recentOutcomes);
        ct.ThrowIfCancellationRequested();

        _strategyOutcomes.AddOrUpdate(
            currentStrategy,
            _ => [.. recentOutcomes],
            (_, existing) => { existing.AddRange(recentOutcomes); return existing; });

        int consecutiveFailures = 0;
        for (int i = recentOutcomes.Count - 1; i >= 0; i--)
        {
            if (!recentOutcomes[i])
                consecutiveFailures++;
            else
                break;
        }

        bool shouldShift = consecutiveFailures >= 3;
        double confidence = shouldShift
            ? Math.Min(0.5 + consecutiveFailures * 0.1, 1.0)
            : 1.0 - consecutiveFailures * 0.2;

        string reason = shouldShift
            ? $"Strategy '{currentStrategy}' has {consecutiveFailures} consecutive failures — shift recommended"
            : $"Strategy '{currentStrategy}' has {consecutiveFailures} consecutive failure(s) — continue";

        string recommended = shouldShift ? "Alternative strategy needed" : currentStrategy;
        return Task.FromResult(new StrategyShiftResult(shouldShift, recommended, Math.Max(confidence, 0.0), reason, 0.0));
    }

    /// <summary>
    /// Generates alternative strategies using SCAMPER operators applied to the failed strategy.
    /// </summary>
    /// <param name="failedStrategy">The strategy that failed.</param>
    /// <param name="context">Context for generating alternatives.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of alternative strategies.</returns>
    public Task<List<AlternativeStrategy>> GenerateAlternativeStrategiesAsync(
        string failedStrategy,
        string context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(failedStrategy);
        ct.ThrowIfCancellationRequested();

        var alternatives = new List<AlternativeStrategy>();
        var rng = new Random(HashCode.Combine(failedStrategy, context));

        foreach (string op in ScamperOperators)
        {
            string description = op switch
            {
                "Substitute" => $"Replace core component of '{failedStrategy}' with an alternative approach",
                "Combine" => $"Merge '{failedStrategy}' with complementary techniques from context",
                "Adapt" => $"Adjust '{failedStrategy}' to better fit the constraints in '{context}'",
                "Modify" => $"Amplify or reduce key parameters of '{failedStrategy}'",
                "Put to another use" => $"Repurpose '{failedStrategy}' for a different aspect of the problem",
                "Eliminate" => $"Remove unnecessary steps from '{failedStrategy}' to simplify",
                "Reverse" => $"Invert the order or logic of '{failedStrategy}'",
                _ => $"Transform '{failedStrategy}' using {op}"
            };

            double effectiveness = 0.3 + rng.NextDouble() * 0.5;
            alternatives.Add(new AlternativeStrategy(description, op, Math.Round(effectiveness, 3)));
        }

        alternatives.Sort((a, b) => b.EstimatedEffectiveness.CompareTo(a.EstimatedEffectiveness));
        return Task.FromResult(alternatives);
    }

    /// <summary>
    /// Estimates the cognitive cost of switching between two tasks.
    /// Cost is higher when tasks share fewer keywords (less overlap).
    /// </summary>
    /// <param name="fromTask">Description of the source task.</param>
    /// <param name="toTask">Description of the target task.</param>
    /// <returns>Estimated switch cost (0–1), where 1 is maximum cost.</returns>
    public double EstimateTaskSwitchCost(string fromTask, string toTask)
    {
        ArgumentNullException.ThrowIfNull(fromTask);
        ArgumentNullException.ThrowIfNull(toTask);

        var fromWords = ExtractKeywords(fromTask);
        var toWords = ExtractKeywords(toTask);

        if (fromWords.Count == 0 && toWords.Count == 0)
            return 0.0;

        int intersection = fromWords.Intersect(toWords, StringComparer.OrdinalIgnoreCase).Count();
        int union = fromWords.Union(toWords, StringComparer.OrdinalIgnoreCase).Count();

        double similarity = union > 0 ? (double)intersection / union : 0.0;
        return Math.Round(1.0 - similarity, 3);
    }

    /// <summary>
    /// Records the outcome of a strategy shift for tracking success rates.
    /// </summary>
    /// <param name="wasSuccessful">Whether the shift led to a positive outcome.</param>
    public void RecordShiftOutcome(bool wasSuccessful)
    {
        lock (_lock)
        {
            _totalShifts++;
            if (wasSuccessful)
                _successfulShifts++;
        }
    }

    /// <summary>
    /// Returns the success rate of past strategy shifts.
    /// </summary>
    public double ShiftSuccessRate => _totalShifts > 0 ? (double)_successfulShifts / _totalShifts : 0.0;

    /// <summary>
    /// Returns the average task-switch cost recorded so far.
    /// </summary>
    public int TotalShifts => _totalShifts;

    private static HashSet<string> ExtractKeywords(string text)
    {
        var words = text.Split([' ', ',', '.', ';', ':', '-', '_', '/', '\\', '(', ')'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return [.. words.Where(w => w.Length > 2).Select(w => w.ToLowerInvariant())];
    }
}

// ==========================================================
// Cognitive Bias Monitor Implementation
// Kahneman Dual Process bias detection and debiasing
// ==========================================================

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Outcome record for tracking bias detection accuracy over time.
/// </summary>
/// <param name="BiasId">The detection being validated.</param>
/// <param name="WasActuallyBiased">Whether the bias was real.</param>
/// <param name="RecordedAt">When the outcome was recorded.</param>
internal sealed record BiasOutcome(
    string BiasId,
    bool WasActuallyBiased,
    DateTime RecordedAt);

/// <summary>
/// Monitors agent reasoning for cognitive biases using keyword and pattern
/// heuristics, and suggests debiased alternatives. Tracks detection accuracy
/// with false positive and true positive rates.
/// </summary>
public sealed class CognitiveBiasMonitor : ICognitiveBiasMonitor
{
    private readonly ConcurrentDictionary<string, BiasDetection> _detections = new();
    private readonly ConcurrentBag<BiasOutcome> _outcomes = new();
    private readonly object _lock = new();

    private static readonly Dictionary<BiasType, string[]> BiasIndicators = new()
    {
        [BiasType.ConfirmationBias] = new[]
        {
            "confirms what i already", "as expected", "i knew it",
            "just as i thought", "proves my point", "consistent with my belief"
        },
        [BiasType.AnchoringBias] = new[]
        {
            "the first", "initially mentioned", "starting point",
            "based on the original", "relative to the initial"
        },
        [BiasType.AvailabilityHeuristic] = new[]
        {
            "i can easily recall", "comes to mind", "i remember when",
            "recently saw", "heard about a case"
        },
        [BiasType.DunningKruger] = new[]
        {
            "i'm certain", "obviously", "clearly simple",
            "trivially", "anyone can see", "no doubt"
        },
        [BiasType.SunkCostFallacy] = new[]
        {
            "already invested", "too late to stop", "come this far",
            "can't waste what we've spent", "put so much into"
        },
        [BiasType.HaloEffect] = new[]
        {
            "since they're good at", "because they succeeded in",
            "given their reputation", "their track record means"
        },
        [BiasType.RecencyBias] = new[]
        {
            "just happened", "most recent", "latest data shows",
            "the last time", "just now"
        },
        [BiasType.FramingEffect] = new[]
        {
            "loss of", "risk of losing", "chance of failure",
            "opportunity to gain", "guaranteed to"
        },
        [BiasType.BandwagonEffect] = new[]
        {
            "everyone is", "most people think", "the consensus is",
            "popular opinion", "widely accepted"
        }
    };

    private static readonly Dictionary<BiasType, string> DebiasingSuggestions = new()
    {
        [BiasType.ConfirmationBias] =
            "Actively seek disconfirming evidence. Ask: what would prove me wrong?",
        [BiasType.AnchoringBias] =
            "Generate the estimate independently before comparing to the anchor value.",
        [BiasType.AvailabilityHeuristic] =
            "Consult base rates and statistical data rather than relying on memorable examples.",
        [BiasType.DunningKruger] =
            "Quantify confidence explicitly. Seek external calibration and peer review.",
        [BiasType.SunkCostFallacy] =
            "Evaluate the decision based only on future costs and benefits, ignoring past expenditures.",
        [BiasType.HaloEffect] =
            "Evaluate each attribute independently. Past success in one area does not guarantee success in another.",
        [BiasType.RecencyBias] =
            "Consider the full historical dataset, not just the most recent observations.",
        [BiasType.FramingEffect] =
            "Restate the problem using both gain and loss framings, then compare conclusions.",
        [BiasType.BandwagonEffect] =
            "Evaluate the evidence independently. Popularity does not equal correctness."
    };

    /// <summary>
    /// Scans reasoning text for indicators of cognitive biases.
    /// </summary>
    /// <param name="reasoning">The reasoning text to analyze.</param>
    /// <param name="context">Optional context for the reasoning.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of detected biases with confidence scores.</returns>
    public Task<Result<IReadOnlyList<BiasDetection>, string>> ScanForBiasesAsync(
        string reasoning,
        string? context = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reasoning))
        {
            return Task.FromResult(
                Result<IReadOnlyList<BiasDetection>, string>.Failure(
                    "Reasoning text must not be empty."));
        }

        string lowerReasoning = reasoning.ToLowerInvariant();
        string? lowerContext = context?.ToLowerInvariant();
        var detections = new List<BiasDetection>();

        foreach ((BiasType biasType, string[] indicators) in BiasIndicators)
        {
            var matchedIndicators = indicators
                .Where(ind => lowerReasoning.Contains(ind))
                .ToList();

            if (matchedIndicators.Count == 0)
                continue;

            // Confidence scales with number of matching indicators
            double confidence = Math.Min(1.0, matchedIndicators.Count * 0.35);

            // Boost confidence if context also contains indicators
            if (lowerContext != null)
            {
                int contextMatches = indicators.Count(ind => lowerContext.Contains(ind));
                confidence = Math.Min(1.0, confidence + contextMatches * 0.1);
            }

            // Apply bias-specific heuristics
            confidence = ApplySpecificHeuristics(biasType, lowerReasoning, confidence);

            string evidence = string.Join("; ", matchedIndicators);
            string correction = DebiasingSuggestions.TryGetValue(biasType, out string? sug)
                ? sug
                : "Review reasoning for potential bias.";

            var detection = new BiasDetection(
                Guid.NewGuid().ToString(),
                biasType,
                confidence,
                evidence,
                correction);

            detections.Add(detection);
            _detections[detection.Id] = detection;
        }

        IReadOnlyList<BiasDetection> result = detections
            .OrderByDescending(d => d.Confidence)
            .ToList()
            .AsReadOnly();

        return Task.FromResult(
            Result<IReadOnlyList<BiasDetection>, string>.Success(result));
    }

    /// <summary>
    /// Suggests a corrected version of the reasoning given a detected bias.
    /// </summary>
    /// <param name="reasoning">The original reasoning text.</param>
    /// <param name="detectedBias">The bias that was detected.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Debiased reasoning suggestion.</returns>
    public Task<Result<string, string>> DebiasAsync(
        string reasoning,
        BiasDetection detectedBias,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reasoning);
        ArgumentNullException.ThrowIfNull(detectedBias);

        var sb = new StringBuilder();
        sb.AppendLine("[Debiased Reasoning]");
        sb.AppendLine($"Original bias detected: {detectedBias.Type} (confidence: {detectedBias.Confidence:F2})");
        sb.AppendLine($"Evidence: {detectedBias.Evidence}");
        sb.AppendLine();
        sb.AppendLine($"Correction strategy: {detectedBias.SuggestedCorrection}");
        sb.AppendLine();
        sb.AppendLine("Revised reasoning should:");

        switch (detectedBias.Type)
        {
            case BiasType.ConfirmationBias:
                sb.AppendLine("- Consider evidence that contradicts the initial hypothesis.");
                sb.AppendLine("- Weight disconfirming evidence equally with confirming evidence.");
                break;
            case BiasType.AnchoringBias:
                sb.AppendLine("- Generate estimates before viewing anchor values.");
                sb.AppendLine("- Use multiple reference points, not just the first one.");
                break;
            case BiasType.RecencyBias:
                sb.AppendLine("- Include data from the full available time range.");
                sb.AppendLine("- Weight historical patterns appropriately.");
                break;
            case BiasType.SunkCostFallacy:
                sb.AppendLine("- Evaluate only prospective costs and benefits.");
                sb.AppendLine("- Ask: if starting fresh, would I make this same choice?");
                break;
            default:
                sb.AppendLine($"- Apply the correction: {detectedBias.SuggestedCorrection}");
                break;
        }

        return Task.FromResult(Result<string, string>.Success(sb.ToString()));
    }

    /// <summary>
    /// Records whether a bias detection turned out to be correct,
    /// for tracking true positive and false positive rates.
    /// </summary>
    /// <param name="biasId">The bias detection ID.</param>
    /// <param name="wasActuallyBiased">Whether the reasoning was truly biased.</param>
    /// <returns>Success if the outcome was recorded.</returns>
    public Result<bool, string> RecordBiasOutcome(string biasId, bool wasActuallyBiased)
    {
        if (!_detections.ContainsKey(biasId))
            return Result<bool, string>.Failure($"No detection found with ID '{biasId}'.");

        _outcomes.Add(new BiasOutcome(biasId, wasActuallyBiased, DateTime.UtcNow));

        return Result<bool, string>.Success(true);
    }

    /// <summary>
    /// Returns the true positive and false positive rates for bias detections.
    /// </summary>
    /// <returns>Tuple of (truePositiveRate, falsePositiveRate, totalOutcomes).</returns>
    public (double TruePositiveRate, double FalsePositiveRate, int TotalOutcomes) GetAccuracyRates()
    {
        List<BiasOutcome> outcomes = _outcomes.ToList();

        if (outcomes.Count == 0)
            return (0.0, 0.0, 0);

        int truePositives = outcomes.Count(o => o.WasActuallyBiased);
        int falsePositives = outcomes.Count(o => !o.WasActuallyBiased);
        int total = outcomes.Count;

        return (
            truePositives / (double)total,
            falsePositives / (double)total,
            total);
    }

    /// <summary>
    /// Returns all detections, optionally filtered by bias type.
    /// </summary>
    /// <param name="biasType">Optional filter by bias type.</param>
    /// <returns>List of bias detections.</returns>
    public IReadOnlyList<BiasDetection> GetDetections(BiasType? biasType = null)
    {
        IEnumerable<BiasDetection> query = _detections.Values;

        if (biasType.HasValue)
            query = query.Where(d => d.Type == biasType.Value);

        return query
            .OrderByDescending(d => d.Confidence)
            .ToList()
            .AsReadOnly();
    }

    private static double ApplySpecificHeuristics(
        BiasType biasType,
        string lowerReasoning,
        double baseConfidence)
    {
        switch (biasType)
        {
            case BiasType.AnchoringBias:
                // Check if the first number mentioned dominates reasoning
                var numbers = Regex.Matches(lowerReasoning, @"\b\d+\.?\d*\b");
                if (numbers.Count >= 2)
                {
                    // If the first number is referenced more than others, boost confidence
                    string firstNumber = numbers[0].Value;
                    int firstCount = Regex.Matches(lowerReasoning, Regex.Escape(firstNumber)).Count;
                    if (firstCount > 1)
                        baseConfidence = Math.Min(1.0, baseConfidence + 0.15);
                }
                break;

            case BiasType.RecencyBias:
                // Check if "last" or "latest" events are weighted disproportionately
                int recencyWords = Regex.Matches(lowerReasoning, @"\b(last|latest|recent|just)\b").Count;
                if (recencyWords >= 3)
                    baseConfidence = Math.Min(1.0, baseConfidence + 0.1);
                break;

            case BiasType.DunningKruger:
                // Lower confidence if hedging language is also present
                bool hasHedging = lowerReasoning.Contains("might") ||
                                  lowerReasoning.Contains("perhaps") ||
                                  lowerReasoning.Contains("uncertain");
                if (hasHedging)
                    baseConfidence *= 0.5;
                break;
        }

        return baseConfidence;
    }
}

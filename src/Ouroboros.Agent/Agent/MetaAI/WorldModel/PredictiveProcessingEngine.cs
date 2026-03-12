// <copyright file="PredictiveProcessingEngine.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Predictive processing engine implementing Friston's Free Energy Principle.
/// Maintains hierarchical predictions at sensory, semantic, and strategic levels,
/// computes precision-weighted prediction errors, and decides between
/// belief updating (perceptual inference) and active inference (acting to
/// reduce surprise). Free energy is the sum of precision-weighted prediction
/// errors across all active predictions.
/// </summary>
public sealed class PredictiveProcessingEngine
{
    /// <summary>Hierarchical prediction level.</summary>
    public enum PredictionLevel
    {
        /// <summary>Low-level sensory predictions (high precision).</summary>
        Sensory,

        /// <summary>Mid-level semantic/conceptual predictions.</summary>
        Semantic,

        /// <summary>High-level strategic/goal predictions (low precision).</summary>
        Strategic,
    }

    /// <summary>A hierarchical prediction with content, level, and precision.</summary>
    /// <param name="Id">Unique prediction identifier.</param>
    /// <param name="Content">Textual content of the prediction.</param>
    /// <param name="Level">Hierarchical level of the prediction.</param>
    /// <param name="Precision">Expected precision (inverse variance) of the prediction.</param>
    /// <param name="Timestamp">When the prediction was generated.</param>
    public sealed record Prediction(
        string Id,
        string Content,
        PredictionLevel Level,
        double Precision,
        DateTime Timestamp);

    /// <summary>The discrepancy between a prediction and an observation.</summary>
    /// <param name="Original">The original prediction.</param>
    /// <param name="Observation">The actual observation that was compared.</param>
    /// <param name="Magnitude">Error magnitude (0 = perfect match, 1 = no overlap).</param>
    /// <param name="PrecisionWeight">Precision weight applied to this error.</param>
    public sealed record PredictionError(
        Prediction Original,
        string Observation,
        double Magnitude,
        double PrecisionWeight);

    /// <summary>Aggregate free energy state with recommended action.</summary>
    /// <param name="TotalFreeEnergy">Sum of precision-weighted prediction errors.</param>
    /// <param name="ActiveErrors">Currently active prediction errors.</param>
    /// <param name="RecommendedAction">"update-beliefs" or "act-to-reduce-surprise".</param>
    public sealed record FreeEnergyState(
        double TotalFreeEnergy,
        List<PredictionError> ActiveErrors,
        string RecommendedAction);

    private readonly List<Prediction> _activePredictions = [];
    private readonly List<PredictionError> _recentErrors = [];
    private readonly double _freeEnergyThreshold;
    private readonly object _lock = new();

    private const int MaxRecentErrors = 200;
    private static readonly TimeSpan ErrorRetentionWindow = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictiveProcessingEngine"/> class.
    /// </summary>
    /// <param name="freeEnergyThreshold">
    /// Threshold above which the engine recommends active inference over belief updating.
    /// </param>
    public PredictiveProcessingEngine(double freeEnergyThreshold = 0.6)
    {
        _freeEnergyThreshold = freeEnergyThreshold;
    }

    /// <summary>
    /// Generates a hierarchical prediction at the specified level.
    /// Higher-level predictions carry lower default precision (more uncertainty).
    /// </summary>
    /// <param name="context">Contextual content for the prediction.</param>
    /// <param name="level">Hierarchical level to generate at.</param>
    /// <returns>The generated prediction.</returns>
    public Prediction GeneratePrediction(string context, PredictionLevel level)
    {
        ArgumentNullException.ThrowIfNull(context);

        var precision = level switch
        {
            PredictionLevel.Sensory => 0.8,
            PredictionLevel.Semantic => 0.6,
            PredictionLevel.Strategic => 0.4,
            _ => 0.5,
        };

        var prediction = new Prediction(
            Guid.NewGuid().ToString("N"),
            context,
            level,
            precision,
            DateTime.UtcNow);

        lock (_lock)
        {
            _activePredictions.Add(prediction);
        }

        return prediction;
    }

    /// <summary>
    /// Computes prediction error by comparing a prediction against an observation.
    /// Magnitude is 1 minus Jaccard similarity of word tokens.
    /// </summary>
    /// <param name="predictionId">The prediction to compare against.</param>
    /// <param name="observation">The actual observation.</param>
    /// <returns>The computed prediction error.</returns>
    public PredictionError ComputeError(string predictionId, string observation)
    {
        ArgumentNullException.ThrowIfNull(predictionId);
        ArgumentNullException.ThrowIfNull(observation);

        Prediction? pred;
        lock (_lock)
        {
            pred = _activePredictions.Find(p => p.Id == predictionId);
        }

        if (pred is null)
        {
            var empty = new Prediction(string.Empty, string.Empty, PredictionLevel.Sensory, 0, DateTime.UtcNow);
            return new PredictionError(empty, observation, 1.0, 0.5);
        }

        var magnitude = 1.0 - ComputeJaccardSimilarity(pred.Content, observation);
        var error = new PredictionError(pred, observation, magnitude, pred.Precision);

        lock (_lock)
        {
            _recentErrors.Add(error);
            PruneErrors();
        }

        return error;
    }

    /// <summary>
    /// Computes aggregate free energy as the sum of precision-weighted prediction errors.
    /// When free energy exceeds the threshold, the system recommends active inference
    /// (acting to change the world); otherwise it recommends belief updating.
    /// </summary>
    /// <returns>The current free energy state with recommended action.</returns>
    public FreeEnergyState ComputeFreeEnergy()
    {
        List<PredictionError> errors;
        lock (_lock)
        {
            errors = [.. _recentErrors];
        }

        var totalFreeEnergy = errors.Sum(e => e.Magnitude * e.PrecisionWeight);
        var action = totalFreeEnergy > _freeEnergyThreshold
            ? "act-to-reduce-surprise"
            : "update-beliefs";

        return new FreeEnergyState(totalFreeEnergy, errors, action);
    }

    /// <summary>
    /// Computes how much beliefs should be updated for a given error.
    /// Low-precision predictions yield larger belief updates (more revisable).
    /// </summary>
    /// <param name="error">The prediction error to compute update magnitude for.</param>
    /// <returns>Belief update magnitude in [0, 1].</returns>
    public static double ComputeBeliefUpdateMagnitude(PredictionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Magnitude * (1.0 - error.PrecisionWeight);
    }

    /// <summary>
    /// Estimates context precision based on familiarity.
    /// More matching active predictions indicate a more familiar (higher-precision) context.
    /// </summary>
    /// <param name="context">The context to estimate precision for.</param>
    /// <returns>Precision estimate in [0.3, 0.95].</returns>
    public double EstimateContextPrecision(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return 0.3;
        }

        int matchCount;
        lock (_lock)
        {
            matchCount = _activePredictions
                .Count(p => p.Content.Contains(context, StringComparison.OrdinalIgnoreCase));
        }

        return Math.Min(0.3 + (matchCount * 0.1), 0.95);
    }

    /// <summary>
    /// Computes Jaccard similarity between two strings based on word tokens.
    /// </summary>
    private static double ComputeJaccardSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return 0;
        }

        var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (wordsA.Count == 0 || wordsB.Count == 0)
        {
            return 0;
        }

        var intersection = wordsA.Intersect(wordsB, StringComparer.OrdinalIgnoreCase).Count();
        var union = wordsA.Union(wordsB, StringComparer.OrdinalIgnoreCase).Count();

        return union == 0 ? 0 : (double)intersection / union;
    }

    /// <summary>Removes stale errors beyond the retention window or capacity limit.</summary>
    private void PruneErrors()
    {
        var cutoff = DateTime.UtcNow - ErrorRetentionWindow;
        _recentErrors.RemoveAll(e => e.Original.Timestamp < cutoff);

        if (_recentErrors.Count > MaxRecentErrors)
        {
            _recentErrors.RemoveRange(0, _recentErrors.Count - MaxRecentErrors);
        }
    }
}

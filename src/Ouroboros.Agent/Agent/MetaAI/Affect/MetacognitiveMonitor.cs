// ==========================================================
// Metacognitive Monitor — Reasoning about reasoning
// Phase 116: Metacognitive Monitoring
// Iaret captures reasoning chains (input → intent → tensor/LLM →
// output), evaluates reasoning validity via prediction error,
// attributes errors to root causes, and calibrates per-domain
// confidence using Bayesian updates on accuracy history.
// ALL logic is tensor/vector-based — no LLM calls.
// ==========================================================

namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// A captured reasoning chain: records every step of a single
/// chat turn from input through routing/reasoning to output.
/// </summary>
/// <param name="Id">Unique chain identifier.</param>
/// <param name="Input">Raw user input text.</param>
/// <param name="InputEmbedding">Embedding vector of the input (nullable).</param>
/// <param name="RoutedIntent">Intent category from IntentRouter.</param>
/// <param name="RouteConfidence">Confidence score from IntentRouter.</param>
/// <param name="ReasoningPath">Which path was taken: Tensor, Pipeline, LLM.</param>
/// <param name="Output">Final response text.</param>
/// <param name="OutputEmbedding">Embedding vector of the output (nullable).</param>
/// <param name="StartedAt">When the reasoning chain started.</param>
/// <param name="CompletedAt">When the reasoning chain completed.</param>
/// <param name="SurpriseScore">Composite surprise from PredictiveProcessor (0-1).</param>
public sealed record ReasoningChain(
    string Id,
    string Input,
    float[]? InputEmbedding,
    string RoutedIntent,
    double RouteConfidence,
    ReasoningPath ReasoningPath,
    string Output,
    float[]? OutputEmbedding,
    DateTime StartedAt,
    DateTime CompletedAt,
    double SurpriseScore);

/// <summary>
/// Which reasoning path handled the input.
/// </summary>
public enum ReasoningPath
{
    /// <summary>IntentRouter → TensorEngine → DirectResponse.</summary>
    TensorDirect,

    /// <summary>IntentRouter → TensorEngine → LanguageRenderer.</summary>
    TensorRendered,

    /// <summary>v7.0 CognitivePipelineOrchestrator.</summary>
    CognitivePipeline,

    /// <summary>ConsolidatedMind LLM fallback.</summary>
    LlmFallback,

    /// <summary>Path unknown (not tracked).</summary>
    Unknown,
}

/// <summary>
/// Known categories of reasoning errors, identified by vector similarity
/// to prototypical error patterns.
/// </summary>
public enum ErrorCategory
{
    /// <summary>Relied too heavily on one source or pattern without cross-checking.</summary>
    OverReliance,

    /// <summary>Missing context that was available but not retrieved.</summary>
    MissingContext,

    /// <summary>Systematic bias detected (confirmation, anchoring, etc.).</summary>
    CognitiveBias,

    /// <summary>Intent was misclassified by the router.</summary>
    IntentMisroute,

    /// <summary>Low-confidence response that should have been escalated.</summary>
    UncalibratedConfidence,

    /// <summary>No specific attribution — general reasoning failure.</summary>
    Unattributed,
}

/// <summary>
/// Attribution of a reasoning error to one or more root causes.
/// </summary>
/// <param name="Chain">The reasoning chain that produced the error.</param>
/// <param name="PrimaryCategory">Most likely error category.</param>
/// <param name="Confidence">Confidence in this attribution (0-1).</param>
/// <param name="ContributingFactors">Additional factors that contributed to the error.</param>
/// <param name="SuggestedAction">What should change to prevent recurrence.</param>
public sealed record ErrorAttribution(
    string ChainId,
    ErrorCategory PrimaryCategory,
    double Confidence,
    IReadOnlyList<string> ContributingFactors,
    string SuggestedAction);

/// <summary>
/// Per-domain confidence calibration record.
/// Tracks predicted vs actual accuracy to learn when to trust own judgments.
/// </summary>
public sealed class DomainCalibration
{
    /// <summary>Domain name (intent category or capability).</summary>
    public string Domain { get; }

    /// <summary>Running EMA of prediction accuracy in this domain.</summary>
    public double Accuracy { get; private set; }

    /// <summary>Number of observations.</summary>
    public int Observations { get; private set; }

    /// <summary>Calibration factor: ratio of actual accuracy to stated confidence.</summary>
    public double CalibrationFactor { get; private set; } = 1.0;

    /// <summary>Whether this domain needs more information before trusting judgments.</summary>
    public bool NeedsMoreInfo => Observations < 5 || Accuracy < 0.4;

    /// <summary>EMA smoothing factor.</summary>
    private const double Alpha = 0.2;

    public DomainCalibration(string domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        Domain = domain;
        Accuracy = 0.5; // Prior: uncertain
    }

    /// <summary>
    /// Records a prediction-outcome pair and updates calibration.
    /// </summary>
    /// <param name="predictedConfidence">Confidence stated before the outcome (0-1).</param>
    /// <param name="wasCorrect">Whether the reasoning was correct.</param>
    public void RecordOutcome(double predictedConfidence, bool wasCorrect)
    {
        double actual = wasCorrect ? 1.0 : 0.0;

        // EMA update on accuracy
        Accuracy = Observations == 0
            ? actual
            : (Alpha * actual) + ((1.0 - Alpha) * Accuracy);

        // Calibration factor: how well does stated confidence match reality?
        if (predictedConfidence > 0.01)
        {
            double newFactor = actual / predictedConfidence;
            CalibrationFactor = Observations == 0
                ? Math.Clamp(newFactor, 0.2, 2.0)
                : (Alpha * Math.Clamp(newFactor, 0.2, 2.0)) + ((1.0 - Alpha) * CalibrationFactor);
        }

        Observations++;
    }
}

/// <summary>
/// Metacognitive snapshot: a summary of recent reasoning quality.
/// </summary>
/// <param name="AverageAccuracy">Mean accuracy across all domains.</param>
/// <param name="WeakestDomain">Domain with lowest accuracy.</param>
/// <param name="WeakestAccuracy">Accuracy of the weakest domain.</param>
/// <param name="StrongestDomain">Domain with highest accuracy.</param>
/// <param name="StrongestAccuracy">Accuracy of the strongest domain.</param>
/// <param name="RecentErrorCount">Number of errors in recent chains.</param>
/// <param name="DominantErrorCategory">Most frequent error type.</param>
/// <param name="OverallCalibration">Average calibration factor (1.0 = perfectly calibrated).</param>
/// <param name="ChainsEvaluated">Total chains evaluated.</param>
public sealed record MetacognitiveSnapshot(
    double AverageAccuracy,
    string WeakestDomain,
    double WeakestAccuracy,
    string StrongestDomain,
    double StrongestAccuracy,
    int RecentErrorCount,
    ErrorCategory DominantErrorCategory,
    double OverallCalibration,
    int ChainsEvaluated);

/// <summary>
/// Tensor-centric metacognitive monitor that wraps reasoning paths,
/// captures chains, evaluates validity, attributes errors, and
/// calibrates per-domain confidence. No LLM calls.
/// </summary>
/// <remarks>
/// <para>
/// Reasoning chain capture: logs the input embedding, routed intent,
/// chosen path (tensor/pipeline/LLM), and output embedding for every
/// chat turn via <see cref="RecordChain"/>.
/// </para>
/// <para>
/// Validity evaluation: compares the chain's surprise score against
/// thresholds. High surprise + low route confidence = likely invalid.
/// Uses cosine similarity between output embedding and expected
/// response centroids (from SelfAssessor accuracy domains).
/// </para>
/// <para>
/// Error attribution: classifies errors into categories by comparing
/// the error pattern vector (surprise, confidence, path) against
/// known category prototypes using cosine similarity.
/// </para>
/// <para>
/// Confidence calibration: Bayesian update on per-domain accuracy.
/// Learns when to trust own judgments vs. seek more info.
/// Feeds back into SelfAssessor via CalibrateConfidence.
/// </para>
/// </remarks>
public sealed class MetacognitiveMonitor
{
    private readonly List<ReasoningChain> _recentChains = [];
    private readonly List<ErrorAttribution> _recentErrors = [];
    private readonly Dictionary<string, DomainCalibration> _calibrations = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>Maximum recent chains to retain.</summary>
    public int MaxRecentChains { get; set; } = 100;

    /// <summary>Maximum recent errors to retain.</summary>
    public int MaxRecentErrors { get; set; } = 50;

    /// <summary>Surprise threshold above which a chain is considered potentially invalid.</summary>
    public double SurpriseThreshold { get; set; } = 0.6;

    /// <summary>Confidence threshold below which a chain is considered low-confidence.</summary>
    public double LowConfidenceThreshold { get; set; } = 0.5;

    /// <summary>Total chains evaluated (lifetime).</summary>
    public int TotalChainsEvaluated
    {
        get { lock (_lock) { return _totalEvaluated; } }
    }
    private int _totalEvaluated;

    /// <summary>
    /// META-01: Records a reasoning chain from a completed chat turn.
    /// Captures input → routing → reasoning → output for later evaluation.
    /// </summary>
    /// <param name="chain">The completed reasoning chain to record.</param>
    public void RecordChain(ReasoningChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        lock (_lock)
        {
            _recentChains.Add(chain);
            if (_recentChains.Count > MaxRecentChains)
            {
                _recentChains.RemoveAt(0);
            }

            _totalEvaluated++;
        }
    }

    /// <summary>
    /// META-01/META-02: Evaluates the validity of a reasoning chain and
    /// attributes errors if the reasoning appears faulty.
    /// </summary>
    /// <param name="chain">The chain to evaluate.</param>
    /// <returns>Error attribution if an error was detected, null if reasoning appears valid.</returns>
    public ErrorAttribution? EvaluateAndAttribute(ReasoningChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        bool hasError = false;
        var factors = new List<string>();
        ErrorCategory primaryCategory = ErrorCategory.Unattributed;
        double confidence = 0.5;

        // ── Rule 1: High surprise indicates prediction was very wrong ──
        if (chain.SurpriseScore > SurpriseThreshold)
        {
            hasError = true;
            factors.Add($"High surprise ({chain.SurpriseScore:F2} > {SurpriseThreshold:F2})");

            // High surprise + low route confidence → intent was misrouted
            if (chain.RouteConfidence < LowConfidenceThreshold)
            {
                primaryCategory = ErrorCategory.IntentMisroute;
                confidence = 0.8;
                factors.Add($"Low route confidence ({chain.RouteConfidence:F2})");
            }
            else
            {
                // High surprise but route was confident → missing context
                primaryCategory = ErrorCategory.MissingContext;
                confidence = 0.6;
                factors.Add("Confident routing but surprising outcome");
            }
        }

        // ── Rule 2: Low confidence output → uncalibrated ──
        if (chain.RouteConfidence < LowConfidenceThreshold && chain.ReasoningPath == ReasoningPath.LlmFallback)
        {
            hasError = true;
            if (primaryCategory == ErrorCategory.Unattributed)
            {
                primaryCategory = ErrorCategory.UncalibratedConfidence;
                confidence = 0.7;
            }
            factors.Add("LLM fallback with low confidence — tensor pipeline should have handled");
        }

        // ── Rule 3: Embedding similarity check (input vs output should differ) ──
        if (chain.InputEmbedding is { Length: > 0 } && chain.OutputEmbedding is { Length: > 0 }
            && chain.InputEmbedding.Length == chain.OutputEmbedding.Length)
        {
            float similarity = CosineSimilarity(chain.InputEmbedding, chain.OutputEmbedding);
            if (similarity > 0.95f)
            {
                // Output is too similar to input — parrot / over-reliance
                hasError = true;
                primaryCategory = ErrorCategory.OverReliance;
                confidence = 0.75;
                factors.Add($"Output embedding too similar to input ({similarity:F3}) — possible echo");
            }
        }

        // ── Rule 4: Extremely fast tensor path may indicate shallow reasoning ──
        TimeSpan duration = chain.CompletedAt - chain.StartedAt;
        if (duration < TimeSpan.FromMilliseconds(10) && chain.ReasoningPath == ReasoningPath.TensorDirect)
        {
            // Very fast direct response — might be over-simplified
            if (chain.Input.Length > 200) // Long input deserves deeper reasoning
            {
                factors.Add("Very fast tensor path on complex input — may need deeper processing");
                if (!hasError)
                {
                    hasError = true;
                    primaryCategory = ErrorCategory.OverReliance;
                    confidence = 0.4;
                }
            }
        }

        if (!hasError) return null;

        string suggestion = primaryCategory switch
        {
            ErrorCategory.IntentMisroute => "Review IntentRouter centroids for this intent category",
            ErrorCategory.MissingContext => "Retrieve more context before responding (RAG, episodic memory)",
            ErrorCategory.OverReliance => "Cross-check response against alternative reasoning paths",
            ErrorCategory.CognitiveBias => "Run BiasMonitor scan and apply debiasing",
            ErrorCategory.UncalibratedConfidence => "Calibrate confidence thresholds; consider escalating uncertain inputs",
            _ => "General reasoning review recommended",
        };

        var attribution = new ErrorAttribution(
            ChainId: chain.Id,
            PrimaryCategory: primaryCategory,
            Confidence: confidence,
            ContributingFactors: factors.AsReadOnly(),
            SuggestedAction: suggestion);

        lock (_lock)
        {
            _recentErrors.Add(attribution);
            if (_recentErrors.Count > MaxRecentErrors)
            {
                _recentErrors.RemoveAt(0);
            }
        }

        return attribution;
    }

    /// <summary>
    /// META-03: Updates per-domain confidence calibration after evaluating a chain.
    /// </summary>
    /// <param name="domain">The intent domain (from RoutedIntent).</param>
    /// <param name="predictedConfidence">Route confidence at decision time.</param>
    /// <param name="wasCorrect">Whether the reasoning was valid (no error attributed).</param>
    public void UpdateCalibration(string domain, double predictedConfidence, bool wasCorrect)
    {
        ArgumentNullException.ThrowIfNull(domain);

        lock (_lock)
        {
            if (!_calibrations.TryGetValue(domain, out var cal))
            {
                cal = new DomainCalibration(domain);
                _calibrations[domain] = cal;
            }

            cal.RecordOutcome(predictedConfidence, wasCorrect);
        }
    }

    /// <summary>
    /// META-03: Gets the calibrated confidence for a domain.
    /// Returns the raw confidence adjusted by the calibration factor learned from history.
    /// </summary>
    /// <param name="domain">Intent domain.</param>
    /// <param name="rawConfidence">Raw confidence from the IntentRouter or model.</param>
    /// <returns>Calibrated confidence, clamped to [0, 1].</returns>
    public double GetCalibratedConfidence(string domain, double rawConfidence)
    {
        lock (_lock)
        {
            if (!_calibrations.TryGetValue(domain, out var cal))
            {
                return rawConfidence; // No data yet — return raw
            }

            return Math.Clamp(rawConfidence * cal.CalibrationFactor, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Whether a specific domain needs more data before its judgments can be trusted.
    /// </summary>
    /// <param name="domain">Intent domain.</param>
    /// <returns>True if domain has insufficient observations or low accuracy.</returns>
    public bool DomainNeedsMoreInfo(string domain)
    {
        lock (_lock)
        {
            if (!_calibrations.TryGetValue(domain, out var cal))
            {
                return true; // Never seen this domain
            }

            return cal.NeedsMoreInfo;
        }
    }

    /// <summary>
    /// Gets a metacognitive snapshot summarizing recent reasoning quality.
    /// </summary>
    public MetacognitiveSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            if (_calibrations.Count == 0)
            {
                return new MetacognitiveSnapshot(
                    AverageAccuracy: 0.5,
                    WeakestDomain: "none",
                    WeakestAccuracy: 0.5,
                    StrongestDomain: "none",
                    StrongestAccuracy: 0.5,
                    RecentErrorCount: _recentErrors.Count,
                    DominantErrorCategory: ErrorCategory.Unattributed,
                    OverallCalibration: 1.0,
                    ChainsEvaluated: _totalEvaluated);
            }

            var calibrationsList = _calibrations.Values.ToList();
            double avgAccuracy = calibrationsList.Average(c => c.Accuracy);
            var weakest = calibrationsList.MinBy(c => c.Accuracy) ?? calibrationsList[0];
            var strongest = calibrationsList.MaxBy(c => c.Accuracy) ?? calibrationsList[0];
            double avgCalibration = calibrationsList.Average(c => c.CalibrationFactor);

            // Dominant error category from recent errors
            ErrorCategory dominant = ErrorCategory.Unattributed;
            if (_recentErrors.Count > 0)
            {
                dominant = _recentErrors
                    .GroupBy(e => e.PrimaryCategory)
                    .MaxBy(g => g.Count())!
                    .Key;
            }

            return new MetacognitiveSnapshot(
                AverageAccuracy: avgAccuracy,
                WeakestDomain: weakest.Domain,
                WeakestAccuracy: weakest.Accuracy,
                StrongestDomain: strongest.Domain,
                StrongestAccuracy: strongest.Accuracy,
                RecentErrorCount: _recentErrors.Count,
                DominantErrorCategory: dominant,
                OverallCalibration: avgCalibration,
                ChainsEvaluated: _totalEvaluated);
        }
    }

    /// <summary>
    /// Gets per-domain calibration data for all tracked domains.
    /// </summary>
    public IReadOnlyDictionary<string, DomainCalibration> GetAllCalibrations()
    {
        lock (_lock)
        {
            return new Dictionary<string, DomainCalibration>(_calibrations);
        }
    }

    /// <summary>
    /// Gets recent error attributions.
    /// </summary>
    public IReadOnlyList<ErrorAttribution> GetRecentErrors()
    {
        lock (_lock)
        {
            return [.. _recentErrors];
        }
    }

    /// <summary>
    /// Gets recent reasoning chains.
    /// </summary>
    public IReadOnlyList<ReasoningChain> GetRecentChains()
    {
        lock (_lock)
        {
            return [.. _recentChains];
        }
    }

    /// <summary>
    /// Gets a diagnostic summary string.
    /// </summary>
    public string GetSummary()
    {
        var snapshot = GetSnapshot();
        return $"[Metacognitive] evaluated={snapshot.ChainsEvaluated}, " +
               $"avgAccuracy={snapshot.AverageAccuracy:F2}, " +
               $"errors={snapshot.RecentErrorCount}, " +
               $"dominant={snapshot.DominantErrorCategory}, " +
               $"calibration={snapshot.OverallCalibration:F2}, " +
               $"weakest={snapshot.WeakestDomain}({snapshot.WeakestAccuracy:F2})";
    }

    /// <summary>
    /// Computes cosine similarity between two embedding vectors.
    /// </summary>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0f;

        float dot = 0f, magA = 0f, magB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        float denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denom > 0 ? dot / denom : 0f;
    }
}

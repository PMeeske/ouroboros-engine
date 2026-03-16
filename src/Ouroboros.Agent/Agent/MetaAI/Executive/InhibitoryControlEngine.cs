// ==========================================================
// Inhibitory Control Engine
// Miyake et al. Executive Functions — response inhibition,
// interference control, and impulse modulation
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI.Executive;

/// <summary>
/// Type of inhibitory control being applied.
/// </summary>
public enum InhibitionType
{
    /// <summary>Suppressing a prepotent or automatic response.</summary>
    ResponseInhibition,

    /// <summary>Filtering out irrelevant or distracting information.</summary>
    InterferenceControl,

    /// <summary>Modulating impulsive tendencies toward premature action.</summary>
    ImpulseModulation
}

/// <summary>
/// Tracks inhibitory control performance metrics.
/// </summary>
public sealed record InhibitionMetrics(
    int TotalEvaluations,
    int CorrectInhibitions,
    int FalseAlarms,
    int Misses,
    double Accuracy);

/// <summary>
/// Implements executive inhibitory control based on Miyake et al.'s model.
/// Evaluates whether proposed actions should be suppressed and tracks
/// inhibition accuracy over time using exponential moving average.
/// </summary>
public sealed class InhibitoryControlEngine
{
    private double _inhibitionStrength = 0.5;
    private int _totalEvaluations;
    private int _correctInhibitions;
    private int _falseAlarms;
    private int _misses;
    private readonly object _lock = new();
    private const double Ema = 0.1;
    private readonly ConcurrentQueue<(DateTime Timestamp, bool Inhibited, bool WasCorrect)> _history = new();

    /// <summary>
    /// Evaluates whether a proposed action should be inhibited.
    /// </summary>
    /// <param name="proposedAction">Description of the action under consideration.</param>
    /// <param name="context">Contextual information for the decision.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="InhibitionResult"/> indicating whether to inhibit.</returns>
    public Task<InhibitionResult> EvaluateResponseInhibitionAsync(
        string proposedAction,
        string context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proposedAction);
        ct.ThrowIfCancellationRequested();

        bool needsDeliberation = context.Contains("uncertain", StringComparison.OrdinalIgnoreCase)
            || context.Contains("risky", StringComparison.OrdinalIgnoreCase)
            || context.Contains("irreversible", StringComparison.OrdinalIgnoreCase)
            || context.Contains("novel", StringComparison.OrdinalIgnoreCase);

        double actionUrgency = EstimateUrgency(proposedAction);
        bool shouldInhibit = actionUrgency < _inhibitionStrength * 1.2 && needsDeliberation;

        double confidence = Math.Abs(_inhibitionStrength * 1.2 - actionUrgency);
        confidence = Math.Min(confidence / _inhibitionStrength, 1.0);

        var delay = shouldInhibit
            ? TimeSpan.FromMilliseconds(500 + (1.0 - actionUrgency) * 2000)
            : TimeSpan.Zero;

        string reason = shouldInhibit
            ? $"Inhibited: urgency ({actionUrgency:F2}) below threshold ({_inhibitionStrength * 1.2:F2}) and context requires deliberation"
            : $"Allowed: urgency ({actionUrgency:F2}) sufficient or no deliberation needed";

        Interlocked.Increment(ref _totalEvaluations);

        return Task.FromResult(new InhibitionResult(shouldInhibit, confidence, reason, delay));
    }

    /// <summary>
    /// Determines whether an impulse should be suppressed given its urgency.
    /// </summary>
    /// <param name="impulse">Description of the impulse.</param>
    /// <param name="urgency">Urgency level of the impulse (0–1).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the impulse should be suppressed.</returns>
    public Task<bool> ShouldSuppressAsync(string impulse, double urgency, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(impulse);
        ct.ThrowIfCancellationRequested();

        bool suppress = urgency < _inhibitionStrength;
        Interlocked.Increment(ref _totalEvaluations);
        return Task.FromResult(suppress);
    }

    /// <summary>
    /// Records the outcome of an inhibition decision for accuracy tracking.
    /// </summary>
    /// <param name="wasInhibited">Whether the action was inhibited.</param>
    /// <param name="wasCorrectDecision">Whether the inhibition decision turned out to be correct.</param>
    public void RecordOutcome(bool wasInhibited, bool wasCorrectDecision)
    {
        _history.Enqueue((DateTime.UtcNow, wasInhibited, wasCorrectDecision));
        while (_history.Count > 1000)
            _history.TryDequeue(out _);

        lock (_lock)
        {
            if (wasCorrectDecision && wasInhibited)
            {
                Interlocked.Increment(ref _correctInhibitions);
                _inhibitionStrength = _inhibitionStrength * (1 - Ema) + 1.0 * Ema;
            }
            else if (!wasCorrectDecision && wasInhibited)
            {
                Interlocked.Increment(ref _falseAlarms);
                _inhibitionStrength = _inhibitionStrength * (1 - Ema) + 0.0 * Ema;
            }
            else if (!wasCorrectDecision && !wasInhibited)
            {
                Interlocked.Increment(ref _misses);
            }

            _inhibitionStrength = Math.Clamp(_inhibitionStrength, 0.1, 0.95);
        }
    }

    /// <summary>
    /// Returns current inhibitory control performance metrics.
    /// </summary>
    public InhibitionMetrics GetMetrics()
    {
        int total = _totalEvaluations;
        double accuracy = total > 0 ? (double)_correctInhibitions / total : 0.0;
        return new InhibitionMetrics(total, _correctInhibitions, _falseAlarms, _misses, accuracy);
    }

    /// <summary>
    /// Returns the current inhibition strength (0–1).
    /// </summary>
    public double InhibitionStrength => _inhibitionStrength;

    private static double EstimateUrgency(string action)
    {
        double urgency = 0.5;
        if (action.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("urgent", StringComparison.OrdinalIgnoreCase))
            urgency += 0.3;
        if (action.Contains("safety", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("emergency", StringComparison.OrdinalIgnoreCase))
            urgency += 0.2;
        if (action.Contains("optional", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("low priority", StringComparison.OrdinalIgnoreCase))
            urgency -= 0.3;
        return Math.Clamp(urgency, 0.0, 1.0);
    }
}

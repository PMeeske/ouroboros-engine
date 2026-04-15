// ==========================================================
// Attention Controller
// Endogenous/exogenous attention with capacity limits
// and fatigue modeling
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI.Executive;

/// <summary>
/// Records an attention capture event.
/// </summary>
/// <param name="StimulusId">Identifier of the capturing stimulus.</param>
/// <param name="Salience">Salience of the stimulus (0–1).</param>
/// <param name="Source">Whether capture was endogenous or exogenous.</param>
/// <param name="Timestamp">When the capture occurred.</param>
public sealed record AttentionCaptureEvent(
    string StimulusId,
    double Salience,
    AttentionSource Source,
    DateTime Timestamp);

/// <summary>
/// Implements an attention controller with Miller's magic number capacity limit,
/// endogenous/exogenous source differentiation, and fatigue modeling with
/// exponential decay during sustained attention.
/// </summary>
public sealed class AttentionController
{
    /// <summary>Miller's magic number + 2: maximum active attention targets (RING-05).</summary>
    private const int MaxCapacity = 9;

    /// <summary>Tolerance range (± 2) around the magic number.</summary>
    private const int CapacityTolerance = 2;

    /// <summary>Priority boost for endogenous (goal-driven) attention.</summary>
    private const double EndogenousBoost = 0.20;

    /// <summary>Fatigue decay constant (higher = faster fatigue).</summary>
    private const double FatigueDecayRate = 0.0005;

    /// <summary>Recovery rate per second of rest.</summary>
    private const double RecoveryRate = 0.01;

    private double _fatigue;
    private DateTime _taskStartTime = DateTime.UtcNow;
    private DateTime? _restStartTime;
    private readonly ConcurrentBag<AttentionCaptureEvent> _captureHistory = new();
    private readonly object _lock = new();

    /// <summary>
    /// Allocates attention across a set of candidate targets, applying capacity
    /// limits (Miller's 7 ± 2) and endogenous priority boosts.
    /// </summary>
    /// <param name="targets">Candidate targets with their raw priorities and sources.</param>
    /// <param name="mode">The attention mode to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="AttentionAllocation"/> with capacity-limited allocations.</returns>
    public Task<AttentionAllocation> AllocateAttentionAsync(
        IReadOnlyList<AttentionTarget> targets,
        AttentionMode mode,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ct.ThrowIfCancellationRequested();

        int effectiveCapacity = mode switch
        {
            AttentionMode.Focused => Math.Max(1, MaxCapacity - CapacityTolerance),
            AttentionMode.Scanning => MaxCapacity + CapacityTolerance,
            _ => MaxCapacity
        };

        var boosted = targets.Select(t =>
        {
            double priority = t.Source == AttentionSource.Endogenous
                ? Math.Min(t.Priority * (1.0 + EndogenousBoost), 1.0)
                : t.Priority;
            return new AttentionTarget(t.Id, t.Description, Math.Round(priority, 4), t.Source);
        })
        .OrderByDescending(t => t.Priority)
        .ToList();

        int dropped = Math.Max(0, boosted.Count - effectiveCapacity);
        var allocated = boosted.Take(effectiveCapacity).ToList();

        double totalCapacity = effectiveCapacity;
        double usedCapacity = allocated.Count;

        // Apply fatigue reduction to quality
        double quality = GetSustainedAttentionQuality();
        var allocations = allocated.ToDictionary(
            t => t.Id,
            t => Math.Round(t.Priority * quality, 4));

        return Task.FromResult(new AttentionAllocation(
            allocations, totalCapacity, usedCapacity));
    }

    /// <summary>
    /// Records an attention capture event for tracking stimulus history.
    /// </summary>
    /// <param name="stimulusId">Identifier of the capturing stimulus.</param>
    /// <param name="salience">Salience of the stimulus (0–1).</param>
    /// <param name="source">Whether the capture was endogenous or exogenous.</param>
    public void RecordAttentionCapture(string stimulusId, double salience, AttentionSource source)
    {
        ArgumentNullException.ThrowIfNull(stimulusId);
        _captureHistory.Add(new AttentionCaptureEvent(stimulusId, salience, source, DateTime.UtcNow));

        // Trim history to prevent unbounded growth
        while (_captureHistory.Count > 500)
            _captureHistory.TryTake(out _);
    }

    /// <summary>
    /// Returns the current sustained attention quality, factoring in fatigue.
    /// Fatigue grows exponentially with time on task and recovers linearly during rest.
    /// </summary>
    /// <returns>Attention quality factor (0–1), where 1.0 means fully alert.</returns>
    public double GetSustainedAttentionQuality()
    {
        lock (_lock)
        {
            UpdateFatigue();
            return Math.Round(1.0 - _fatigue, 4);
        }
    }

    /// <summary>
    /// Signals the start of a rest period, during which fatigue recovers linearly.
    /// </summary>
    public void BeginRest()
    {
        lock (_lock)
        {
            UpdateFatigue();
            _restStartTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Signals the end of a rest period and resumes active attention.
    /// </summary>
    public void EndRest()
    {
        lock (_lock)
        {
            if (_restStartTime.HasValue)
            {
                double restSeconds = (DateTime.UtcNow - _restStartTime.Value).TotalSeconds;
                _fatigue = Math.Max(0.0, _fatigue - restSeconds * RecoveryRate);
                _restStartTime = null;
            }
            _taskStartTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Returns the number of attention capture events recorded.
    /// </summary>
    public int CaptureEventCount => _captureHistory.Count;

    /// <summary>
    /// Returns the current fatigue level (0–1).
    /// </summary>
    public double FatigueLevel
    {
        get
        {
            lock (_lock)
            {
                UpdateFatigue();
                return Math.Round(_fatigue, 4);
            }
        }
    }

    private void UpdateFatigue()
    {
        if (_restStartTime.HasValue)
            return;

        double secondsOnTask = (DateTime.UtcNow - _taskStartTime).TotalSeconds;
        _fatigue = Math.Clamp(1.0 - Math.Exp(-FatigueDecayRate * secondsOnTask), 0.0, 1.0);
    }
}

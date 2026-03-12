// ==========================================================
// Cognitive Fatigue Model
// Phase 3: Affective Dynamics - Fatigue accumulation and recovery
// Extends homeostasis with exponential fatigue tracking
// ==========================================================

namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// Record of a single cognitive effort event.
/// </summary>
/// <param name="TaskId">Identifier of the task that caused the effort.</param>
/// <param name="Complexity">Complexity of the cognitive task (0.0 to 1.0).</param>
/// <param name="Duration">Duration of the cognitive effort.</param>
/// <param name="Timestamp">When the effort was recorded.</param>
public sealed record CognitiveEffortEvent(
    string TaskId,
    double Complexity,
    TimeSpan Duration,
    DateTime Timestamp);

/// <summary>
/// Snapshot of the current fatigue state.
/// </summary>
/// <param name="FatigueLevel">Current fatigue level (0.0 to 1.0).</param>
/// <param name="CumulativeEffort">Total accumulated effort units.</param>
/// <param name="RecoveryEstimate">Estimated time to recover to baseline fatigue.</param>
/// <param name="IsHighFatigue">Whether fatigue exceeds the high-fatigue threshold (0.7).</param>
/// <param name="LastEffortTimestamp">When the last cognitive effort was recorded.</param>
public sealed record FatigueSnapshot(
    double FatigueLevel,
    double CumulativeEffort,
    TimeSpan RecoveryEstimate,
    bool IsHighFatigue,
    DateTime? LastEffortTimestamp);

/// <summary>
/// Models cognitive fatigue accumulation and recovery for AI agents.
/// Fatigue follows an exponential accumulation curve: fatigue = 1 - exp(-effort * decayRate).
/// Recovery occurs linearly during idle periods.
/// Designed to integrate with <see cref="HomeostasisPolicy"/> as a SignalType.Stress signal source.
/// </summary>
public sealed class CognitiveFatigueModel
{
    /// <summary>
    /// Fatigue level above which the agent is considered highly fatigued.
    /// </summary>
    public const double HighFatigueThreshold = 0.7;

    private readonly double _decayRate;
    private readonly double _recoveryRate;
    private readonly List<CognitiveEffortEvent> _effortHistory = [];
    private readonly object _lock = new();

    private double _cumulativeEffort;
    private DateTime _lastActivityTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="CognitiveFatigueModel"/> class.
    /// </summary>
    /// <param name="decayRate">
    /// Controls how quickly fatigue accumulates with effort.
    /// Higher values mean faster fatigue onset. Default is 0.5.
    /// </param>
    /// <param name="recoveryRate">
    /// Linear recovery rate per minute of idle time.
    /// Effort units recovered per minute. Default is 0.1.
    /// </param>
    public CognitiveFatigueModel(double decayRate = 0.5, double recoveryRate = 0.1)
    {
        _decayRate = Math.Clamp(decayRate, 0.01, 5.0);
        _recoveryRate = Math.Clamp(recoveryRate, 0.001, 1.0);
        _cumulativeEffort = 0.0;
        _lastActivityTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a cognitive effort event, accumulating fatigue.
    /// Effort is computed as complexity multiplied by duration in minutes.
    /// </summary>
    /// <param name="taskId">Identifier of the task.</param>
    /// <param name="complexity">Complexity of the cognitive task (0.0 to 1.0).</param>
    /// <param name="duration">Duration of the effort.</param>
    public void RecordCognitiveEffort(string taskId, double complexity, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        complexity = Math.Clamp(complexity, 0.0, 1.0);

        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        lock (_lock)
        {
            // Apply recovery for idle time since last activity
            ApplyRecoveryUnlocked();

            // Accumulate effort: complexity * duration_in_minutes
            var effortUnits = complexity * duration.TotalMinutes;
            _cumulativeEffort += effortUnits;
            _lastActivityTime = DateTime.UtcNow;

            _effortHistory.Add(new CognitiveEffortEvent(
                TaskId: taskId,
                Complexity: complexity,
                Duration: duration,
                Timestamp: DateTime.UtcNow));
        }
    }

    /// <summary>
    /// Gets the current fatigue level using exponential accumulation.
    /// Formula: fatigue = 1 - exp(-cumulativeEffort * decayRate).
    /// Applies recovery for any idle time before computing.
    /// </summary>
    /// <returns>Current fatigue level (0.0 to 1.0).</returns>
    public double GetFatigueLevel()
    {
        lock (_lock)
        {
            ApplyRecoveryUnlocked();
            return ComputeFatigue();
        }
    }

    /// <summary>
    /// Estimates the time required to recover to baseline fatigue (below 0.1).
    /// </summary>
    /// <returns>Estimated recovery time, or <see cref="TimeSpan.Zero"/> if already recovered.</returns>
    public TimeSpan GetRecoveryEstimate()
    {
        lock (_lock)
        {
            ApplyRecoveryUnlocked();

            if (_cumulativeEffort <= 0.01)
                return TimeSpan.Zero;

            // Time to recover = cumulative_effort / recovery_rate (in minutes)
            var minutesToRecover = _cumulativeEffort / _recoveryRate;
            return TimeSpan.FromMinutes(minutesToRecover);
        }
    }

    /// <summary>
    /// Gets a complete snapshot of the current fatigue state.
    /// </summary>
    /// <returns>A fatigue snapshot with all relevant metrics.</returns>
    public FatigueSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            ApplyRecoveryUnlocked();
            var fatigue = ComputeFatigue();
            var recoveryEstimate = _cumulativeEffort > 0.01
                ? TimeSpan.FromMinutes(_cumulativeEffort / _recoveryRate)
                : TimeSpan.Zero;

            var lastTimestamp = _effortHistory.Count > 0
                ? _effortHistory[^1].Timestamp
                : (DateTime?)null;

            return new FatigueSnapshot(
                FatigueLevel: Math.Round(fatigue, 4),
                CumulativeEffort: Math.Round(_cumulativeEffort, 4),
                RecoveryEstimate: recoveryEstimate,
                IsHighFatigue: fatigue >= HighFatigueThreshold,
                LastEffortTimestamp: lastTimestamp);
        }
    }

    /// <summary>
    /// Generates a stress signal value suitable for feeding into
    /// <see cref="IValenceMonitor.RecordSignal"/> with <see cref="SignalType.Stress"/>.
    /// Maps fatigue level directly to a 0.0-1.0 stress signal.
    /// </summary>
    /// <returns>Stress signal value derived from current fatigue.</returns>
    public double GetStressSignal()
    {
        return GetFatigueLevel();
    }

    /// <summary>
    /// Resets the fatigue model to its initial state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _cumulativeEffort = 0.0;
            _lastActivityTime = DateTime.UtcNow;
            _effortHistory.Clear();
        }
    }

    /// <summary>
    /// Gets the effort event history.
    /// </summary>
    /// <param name="count">Maximum number of recent events to return.</param>
    /// <returns>Recent effort events ordered by time descending.</returns>
    public List<CognitiveEffortEvent> GetEffortHistory(int count = 50)
    {
        lock (_lock)
        {
            return _effortHistory
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    /// <summary>
    /// Applies linear recovery based on idle time since the last activity.
    /// Must be called from within a lock(_lock) block.
    /// </summary>
    private void ApplyRecoveryUnlocked()
    {
        var now = DateTime.UtcNow;
        var idleMinutes = (now - _lastActivityTime).TotalMinutes;

        if (idleMinutes > 0.0)
        {
            var recovery = idleMinutes * _recoveryRate;
            _cumulativeEffort = Math.Max(0.0, _cumulativeEffort - recovery);
            _lastActivityTime = now;
        }
    }

    /// <summary>
    /// Computes fatigue using the exponential accumulation formula.
    /// Must be called from within a lock(_lock) block.
    /// </summary>
    private double ComputeFatigue()
    {
        return Math.Clamp(1.0 - Math.Exp(-_cumulativeEffort * _decayRate), 0.0, 1.0);
    }
}

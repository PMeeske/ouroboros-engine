using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Real-time cognitive monitor implementing ICognitiveMonitor.
/// Provides thread-safe event recording, sliding window metrics, and pattern detection.
/// </summary>
public sealed class RealtimeCognitiveMonitor : ICognitiveMonitor, IDisposable
{
    private readonly ConcurrentQueue<CognitiveEvent> eventBuffer;
    private readonly ConcurrentDictionary<Guid, MonitoringAlert> activeAlerts;
    private readonly ConcurrentDictionary<string, double> thresholds;
    private readonly ConcurrentBag<Action<MonitoringAlert>> subscribers;
    private readonly object metricsLock = new();

    private readonly int maxBufferSize;
    private readonly TimeSpan slidingWindowDuration;

    private int totalEventsRecorded;
    private int errorCount;
    private long totalLatencyTicks;
    private int latencyMeasurements;
    private DateTime lastHealthCheck;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeCognitiveMonitor"/> class.
    /// </summary>
    /// <param name="maxBufferSize">Maximum number of events to retain in buffer.</param>
    /// <param name="slidingWindowDuration">Duration of the sliding window for metrics.</param>
    public RealtimeCognitiveMonitor(int maxBufferSize = 1000, TimeSpan? slidingWindowDuration = null)
    {
        this.maxBufferSize = maxBufferSize;
        this.slidingWindowDuration = slidingWindowDuration ?? TimeSpan.FromMinutes(5);
        this.eventBuffer = new ConcurrentQueue<CognitiveEvent>();
        this.activeAlerts = new ConcurrentDictionary<Guid, MonitoringAlert>();
        this.thresholds = new ConcurrentDictionary<string, double>();
        this.subscribers = new ConcurrentBag<Action<MonitoringAlert>>();
        this.lastHealthCheck = DateTime.UtcNow;

        InitializeDefaultThresholds();
    }

    /// <inheritdoc/>
    public Result<Unit, string> RecordEvent(CognitiveEvent cognitiveEvent)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (cognitiveEvent is null)
        {
            return Result<Unit, string>.Failure("Cognitive event cannot be null.");
        }

        try
        {
            // Add event to buffer
            eventBuffer.Enqueue(cognitiveEvent);
            Interlocked.Increment(ref totalEventsRecorded);

            // Trim buffer if needed
            TrimBuffer();

            // Update metrics
            UpdateMetrics(cognitiveEvent);

            // Check for anomalies and generate alerts
            DetectAnomalies(cognitiveEvent);

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to record event: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public CognitiveHealth GetHealth()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        lock (metricsLock)
        {
            var recentEvents = GetEventsInWindow();
            var errorEvents = recentEvents.Count(e => e.EventType == CognitiveEventType.ErrorDetected);
            var totalInWindow = recentEvents.Count;

            var errorRate = totalInWindow > 0 ? (double)errorEvents / totalInWindow : 0.0;
            var efficiency = CalculateEfficiency(recentEvents);
            var healthScore = CalculateHealthScore(errorRate, efficiency);
            var avgLatency = latencyMeasurements > 0
                ? TimeSpan.FromTicks(totalLatencyTicks / latencyMeasurements)
                : TimeSpan.Zero;

            lastHealthCheck = DateTime.UtcNow;

            return CognitiveHealth.FromMetrics(
                healthScore,
                efficiency,
                errorRate,
                avgLatency,
                activeAlerts.Values.ToImmutableList());
        }
    }

    /// <inheritdoc/>
    public ImmutableList<CognitiveEvent> GetRecentEvents(int count)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return eventBuffer
            .OrderByDescending(e => e.Timestamp)
            .Take(Math.Max(0, count))
            .ToImmutableList();
    }

    /// <inheritdoc/>
    public ImmutableList<MonitoringAlert> GetAlerts()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return activeAlerts.Values
            .OrderByDescending(a => a.Priority)
            .ThenByDescending(a => a.Timestamp)
            .ToImmutableList();
    }

    /// <inheritdoc/>
    public Result<Unit, string> AcknowledgeAlert(Guid alertId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (activeAlerts.TryRemove(alertId, out _))
        {
            return Result<Unit, string>.Success(Unit.Value);
        }

        return Result<Unit, string>.Failure($"Alert with ID {alertId} not found.");
    }

    /// <inheritdoc/>
    public Result<Unit, string> SetThreshold(string metric, double threshold)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (string.IsNullOrWhiteSpace(metric))
        {
            return Result<Unit, string>.Failure("Metric name cannot be empty.");
        }

        if (threshold < 0)
        {
            return Result<Unit, string>.Failure($"Threshold must be non-negative, got {threshold}.");
        }

        thresholds[metric] = threshold;
        return Result<Unit, string>.Success(Unit.Value);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(Action<MonitoringAlert> handler)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        subscribers.Add(handler);
        return new AlertSubscription(this, handler);
    }

    /// <summary>
    /// Gets the current threshold for a metric.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <returns>An Option containing the threshold if set.</returns>
    public Option<double> GetThreshold(string metric)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return thresholds.TryGetValue(metric, out var threshold)
            ? Option<double>.Some(threshold)
            : Option<double>.None();
    }

    /// <summary>
    /// Clears all recorded events and resets metrics.
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        while (eventBuffer.TryDequeue(out _))
        {
        }

        activeAlerts.Clear();

        lock (metricsLock)
        {
            totalEventsRecorded = 0;
            errorCount = 0;
            totalLatencyTicks = 0;
            latencyMeasurements = 0;
        }
    }

    /// <summary>
    /// Disposes resources used by the monitor.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        // Clear buffers before marking as disposed
        while (eventBuffer.TryDequeue(out _))
        {
        }

        activeAlerts.Clear();

        disposed = true;
    }

    private void InitializeDefaultThresholds()
    {
        thresholds["error_rate"] = 0.1;
        thresholds["consecutive_errors"] = 3;
        thresholds["confusion_rate"] = 0.2;
        thresholds["contradiction_limit"] = 2;
        thresholds["latency_ms"] = 1000;
    }

    private void TrimBuffer()
    {
        while (eventBuffer.Count > maxBufferSize && eventBuffer.TryDequeue(out _))
        {
        }
    }

    private void UpdateMetrics(CognitiveEvent cognitiveEvent)
    {
        lock (metricsLock)
        {
            if (cognitiveEvent.EventType == CognitiveEventType.ErrorDetected)
            {
                Interlocked.Increment(ref errorCount);
            }

            // Track latency if present in context
            if (cognitiveEvent.Context.TryGetValue("latency_ms", out var latencyObj) &&
                latencyObj is double latencyMs)
            {
                totalLatencyTicks += TimeSpan.FromMilliseconds(latencyMs).Ticks;
                Interlocked.Increment(ref latencyMeasurements);
            }
        }
    }

    private void DetectAnomalies(CognitiveEvent cognitiveEvent)
    {
        var recentEvents = GetEventsInWindow();

        // Check for consecutive errors
        if (thresholds.TryGetValue("consecutive_errors", out var consecutiveErrorThreshold))
        {
            var consecutiveErrors = CountConsecutiveErrors(recentEvents);
            if (consecutiveErrors >= (int)consecutiveErrorThreshold)
            {
                var alert = MonitoringAlert.HighPriority(
                    "ConsecutiveErrors",
                    $"Detected {consecutiveErrors} consecutive errors",
                    recentEvents.Where(e => e.EventType == CognitiveEventType.ErrorDetected).TakeLast(consecutiveErrors),
                    "Review error causes and consider pausing processing");
                RaiseAlert(alert);
            }
        }

        // Check for contradiction detection
        if (cognitiveEvent.EventType == CognitiveEventType.Contradiction &&
            thresholds.TryGetValue("contradiction_limit", out var contradictionLimit))
        {
            var recentContradictions = recentEvents.Count(e => e.EventType == CognitiveEventType.Contradiction);
            if (recentContradictions >= (int)contradictionLimit)
            {
                var alert = MonitoringAlert.HighPriority(
                    "ContradictionOverload",
                    $"Detected {recentContradictions} contradictions in recent processing",
                    recentEvents.Where(e => e.EventType == CognitiveEventType.Contradiction),
                    "Review reasoning chain and resolve contradictions");
                RaiseAlert(alert);
            }
        }

        // Check for high confusion rate
        if (cognitiveEvent.EventType == CognitiveEventType.ConfusionSensed &&
            thresholds.TryGetValue("confusion_rate", out var confusionRateThreshold))
        {
            var confusionRate = recentEvents.Count > 0
                ? (double)recentEvents.Count(e => e.EventType == CognitiveEventType.ConfusionSensed) / recentEvents.Count
                : 0.0;

            if (confusionRate >= confusionRateThreshold)
            {
                var alert = MonitoringAlert.MediumPriority(
                    "HighConfusionRate",
                    $"Confusion rate ({confusionRate:P1}) exceeds threshold ({confusionRateThreshold:P1})",
                    recentEvents.Where(e => e.EventType == CognitiveEventType.ConfusionSensed),
                    "Consider providing additional context or simplifying task");
                RaiseAlert(alert);
            }
        }

        // Check critical severity events
        if (cognitiveEvent.Severity == Severity.Critical)
        {
            var alert = MonitoringAlert.HighPriority(
                "CriticalEvent",
                $"Critical cognitive event detected: {cognitiveEvent.Description}",
                new[] { cognitiveEvent },
                "Immediate attention required - review event context");
            RaiseAlert(alert);
        }
    }

    private ImmutableList<CognitiveEvent> GetEventsInWindow()
    {
        var windowStart = DateTime.UtcNow - slidingWindowDuration;
        return eventBuffer
            .Where(e => e.Timestamp >= windowStart)
            .OrderBy(e => e.Timestamp)
            .ToImmutableList();
    }

    private static int CountConsecutiveErrors(ImmutableList<CognitiveEvent> events)
    {
        var consecutive = 0;
        var maxConsecutive = 0;

        foreach (var evt in events.OrderBy(e => e.Timestamp))
        {
            if (evt.EventType == CognitiveEventType.ErrorDetected)
            {
                consecutive++;
                maxConsecutive = Math.Max(maxConsecutive, consecutive);
            }
            else
            {
                consecutive = 0;
            }
        }

        return maxConsecutive;
    }

    private static double CalculateEfficiency(ImmutableList<CognitiveEvent> recentEvents)
    {
        if (recentEvents.IsEmpty)
        {
            return 1.0;
        }

        var productiveEvents = recentEvents.Count(e =>
            e.EventType == CognitiveEventType.ThoughtGenerated ||
            e.EventType == CognitiveEventType.DecisionMade ||
            e.EventType == CognitiveEventType.InsightGained ||
            e.EventType == CognitiveEventType.GoalCompleted);

        var problemEvents = recentEvents.Count(e =>
            e.EventType == CognitiveEventType.ErrorDetected ||
            e.EventType == CognitiveEventType.ConfusionSensed ||
            e.EventType == CognitiveEventType.Contradiction);

        var total = productiveEvents + problemEvents;
        return total > 0 ? (double)productiveEvents / total : 1.0;
    }

    private static double CalculateHealthScore(double errorRate, double efficiency)
    {
        // Health score is a weighted combination of error rate and efficiency
        var errorComponent = 1.0 - Math.Min(1.0, errorRate * 2.0); // Error rate has double weight
        var efficiencyComponent = efficiency;

        return (errorComponent * 0.6) + (efficiencyComponent * 0.4);
    }

    private void RaiseAlert(MonitoringAlert alert)
    {
        // Add to active alerts (avoid duplicates by alert type in short window)
        var recentSameType = activeAlerts.Values
            .Any(a => a.AlertType == alert.AlertType &&
                      (DateTime.UtcNow - a.Timestamp) < TimeSpan.FromSeconds(30));

        if (!recentSameType)
        {
            activeAlerts[alert.Id] = alert;

            // Notify subscribers
            foreach (var subscriber in subscribers)
            {
                try
                {
                    subscriber(alert);
                }
                catch
                {
                    // Ignore subscriber errors to prevent cascade
                }
            }
        }
    }

    /// <summary>
    /// Subscription handle for alert notifications.
    /// </summary>
    private sealed class AlertSubscription : IDisposable
    {
        private readonly RealtimeCognitiveMonitor monitor;
        private readonly Action<MonitoringAlert> handler;
        private bool disposed;

        public AlertSubscription(RealtimeCognitiveMonitor monitor, Action<MonitoringAlert> handler)
        {
            this.monitor = monitor;
            this.handler = handler;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            // Note: ConcurrentBag doesn't support removal, so we rely on checking disposed state
            // In production, consider using a different collection type
        }
    }
}
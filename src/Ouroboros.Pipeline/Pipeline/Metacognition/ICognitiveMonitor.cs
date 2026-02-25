using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Interface for cognitive monitoring operations.
/// Provides methods for recording events, querying health, and managing alerts.
/// </summary>
public interface ICognitiveMonitor
{
    /// <summary>
    /// Records a cognitive event for monitoring.
    /// </summary>
    /// <param name="cognitiveEvent">The event to record.</param>
    /// <returns>A Result indicating success or error.</returns>
    Result<Unit, string> RecordEvent(CognitiveEvent cognitiveEvent);

    /// <summary>
    /// Gets the current cognitive health status.
    /// </summary>
    /// <returns>The current CognitiveHealth.</returns>
    CognitiveHealth GetHealth();

    /// <summary>
    /// Gets the most recent cognitive events.
    /// </summary>
    /// <param name="count">The maximum number of events to return.</param>
    /// <returns>An immutable list of recent events.</returns>
    ImmutableList<CognitiveEvent> GetRecentEvents(int count);

    /// <summary>
    /// Gets all active monitoring alerts.
    /// </summary>
    /// <returns>An immutable list of active alerts.</returns>
    ImmutableList<MonitoringAlert> GetAlerts();

    /// <summary>
    /// Acknowledges and dismisses an alert.
    /// </summary>
    /// <param name="alertId">The ID of the alert to acknowledge.</param>
    /// <returns>A Result indicating success or error.</returns>
    Result<Unit, string> AcknowledgeAlert(Guid alertId);

    /// <summary>
    /// Sets a threshold for a specific metric that triggers alerts.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="threshold">The threshold value.</param>
    /// <returns>A Result indicating success or error.</returns>
    Result<Unit, string> SetThreshold(string metric, double threshold);

    /// <summary>
    /// Subscribes to alert notifications.
    /// </summary>
    /// <param name="handler">The handler to call when alerts are generated.</param>
    /// <returns>A disposable subscription that can be used to unsubscribe.</returns>
    IDisposable Subscribe(Action<MonitoringAlert> handler);
}
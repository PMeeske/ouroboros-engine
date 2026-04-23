namespace Ouroboros.Providers;

/// <summary>
/// Abstract interface for tracking LLM API usage costs.
/// Allows swapping implementations (in-memory, persistent, aggregated, etc.).
/// </summary>
public interface ICostTracker
{
    /// <summary>
    /// Start timing a new request.
    /// </summary>
    void StartRequest();

    /// <summary>
    /// Record completion of a request with token counts.
    /// </summary>
    /// <returns>Metrics for the completed request.</returns>
    RequestMetrics EndRequest(int inputTokens, int outputTokens);

    /// <summary>
    /// Get aggregated metrics for the current session.
    /// </summary>
    SessionMetrics GetSessionMetrics();

    /// <summary>
    /// Reset session totals.
    /// </summary>
    void Reset();

    /// <summary>
    /// Format a human-readable session summary.
    /// </summary>
    string FormatSessionSummary();

    /// <summary>
    /// Get a brief cost string for inline display.
    /// </summary>
    string GetCostString();
}

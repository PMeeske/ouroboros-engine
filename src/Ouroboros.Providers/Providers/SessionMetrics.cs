using System.Globalization;

namespace Ouroboros.Providers;

/// <summary>
/// Aggregate metrics for a session.
/// </summary>
public sealed record SessionMetrics(
    string Model,
    string Provider,
    int TotalRequests,
    long TotalInputTokens,
    long TotalOutputTokens,
    TimeSpan TotalLatency,
    decimal TotalCost,
    TimeSpan AverageLatency)
{
    public long TotalTokens => TotalInputTokens + TotalOutputTokens;

    public string ToCostString()
    {
        if (TotalCost == 0)
            return $"{TotalTokens.ToString("N0", CultureInfo.InvariantCulture)} tokens";
        return $"{TotalTokens.ToString("N0", CultureInfo.InvariantCulture)} tokens (${TotalCost.ToString("F4", CultureInfo.InvariantCulture)})";
    }
}
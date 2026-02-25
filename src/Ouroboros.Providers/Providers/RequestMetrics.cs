using System.Globalization;

namespace Ouroboros.Providers;

/// <summary>
/// Metrics for a single request.
/// </summary>
public sealed record RequestMetrics(
    string Model,
    int InputTokens,
    int OutputTokens,
    TimeSpan Latency,
    decimal Cost,
    DateTime Timestamp)
{
    public int TotalTokens => InputTokens + OutputTokens;
    public double TokensPerSecond => Latency.TotalSeconds > 0 ? OutputTokens / Latency.TotalSeconds : 0;

    public override string ToString()
    {
        if (Cost == 0)
            return $"{InputTokens}→{OutputTokens} tokens, {Latency.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)}s ({TokensPerSecond.ToString("F0", CultureInfo.InvariantCulture)} tok/s)";
        return $"{InputTokens}→{OutputTokens} tokens, ${Cost.ToString("F4", CultureInfo.InvariantCulture)}, {Latency.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)}s ({TokensPerSecond.ToString("F0", CultureInfo.InvariantCulture)} tok/s)";
    }
}
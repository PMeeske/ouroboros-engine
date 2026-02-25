namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Cache statistics for monitoring.
/// </summary>
public sealed record CacheStatistics(
    int TotalEntries,
    int MaxEntries,
    long HitCount,
    long MissCount,
    double HitRate,
    long MemoryEstimateBytes)
{
    /// <summary>Percentage of cache capacity used.</summary>
    public double UtilizationPercent => MaxEntries > 0 ? (double)TotalEntries / MaxEntries * 100 : 0;

    /// <summary>Whether the cache is healthy (hit rate > 50% or still warming up).</summary>
    public bool IsHealthy => HitRate > 0.5 || (HitCount + MissCount) < 100;
}
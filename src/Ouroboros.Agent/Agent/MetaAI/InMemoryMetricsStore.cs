#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// In-Memory Metrics Store Implementation
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// In-memory metrics store for testing and development.
/// Does not persist across restarts.
/// </summary>
public sealed class InMemoryMetricsStore : IMetricsStore
{
    private readonly Dictionary<string, PerformanceMetrics> _metrics = new();
    private readonly object _lock = new();

    public Task StoreMetricsAsync(PerformanceMetrics metrics, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        lock (_lock)
        {
            _metrics[metrics.ResourceName] = metrics;
        }

        return Task.CompletedTask;
    }

    public Task<PerformanceMetrics?> GetMetricsAsync(string resourceName, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_metrics.TryGetValue(resourceName, out var metrics) ? metrics : null);
        }
    }

    public Task<IReadOnlyDictionary<string, PerformanceMetrics>> GetAllMetricsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyDictionary<string, PerformanceMetrics>>(
                new Dictionary<string, PerformanceMetrics>(_metrics));
        }
    }

    public Task<bool> RemoveMetricsAsync(string resourceName, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_metrics.Remove(resourceName));
        }
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _metrics.Clear();
        }

        return Task.CompletedTask;
    }

    public Task<MetricsStoreStatistics> GetStatisticsAsync()
    {
        lock (_lock)
        {
            if (_metrics.Count == 0)
            {
                return Task.FromResult(new MetricsStoreStatistics(
                    TotalResources: 0,
                    TotalExecutions: 0,
                    OverallSuccessRate: 0,
                    AverageLatencyMs: 0,
                    OldestMetric: null,
                    NewestMetric: null));
            }

            var allMetrics = _metrics.Values.ToList();

            return Task.FromResult(new MetricsStoreStatistics(
                TotalResources: allMetrics.Count,
                TotalExecutions: allMetrics.Sum(m => m.ExecutionCount),
                OverallSuccessRate: allMetrics.Average(m => m.SuccessRate),
                AverageLatencyMs: allMetrics.Average(m => m.AverageLatencyMs),
                OldestMetric: allMetrics.Min(m => m.LastUsed),
                NewestMetric: allMetrics.Max(m => m.LastUsed)));
        }
    }
}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent;

/// <summary>
/// Partial class containing metrics recording and persistence.
/// </summary>
public sealed partial class SmartModelOrchestrator
{
    /// <summary>
    /// Records performance metrics for model execution.
    /// </summary>
    public void RecordMetric(string resourceName, double latencyMs, bool success)
    {
        PerformanceMetrics updatedMetrics = _metrics.AddOrUpdate(
            resourceName,
            // Add new
            _ => new PerformanceMetrics(
                resourceName,
                ExecutionCount: 1,
                AverageLatencyMs: latencyMs,
                SuccessRate: success ? 1.0 : 0.0,
                LastUsed: DateTime.UtcNow,
                CustomMetrics: new Dictionary<string, double>()),
            // Update existing
            (_, existing) =>
            {
                int newCount = existing.ExecutionCount + 1;
                double newAvgLatency = ((existing.AverageLatencyMs * existing.ExecutionCount) + latencyMs) / newCount;
                double newSuccessRate = ((existing.SuccessRate * existing.ExecutionCount) + (success ? 1.0 : 0.0)) / newCount;

                return new PerformanceMetrics(
                    resourceName,
                    ExecutionCount: newCount,
                    AverageLatencyMs: newAvgLatency,
                    SuccessRate: newSuccessRate,
                    LastUsed: DateTime.UtcNow,
                    CustomMetrics: existing.CustomMetrics);
            });

        // Persist to store if available (fire and forget for performance)
        _metricsStore?.StoreMetricsAsync(updatedMetrics);
    }

    /// <summary>
    /// Records performance metrics for model execution asynchronously.
    /// </summary>
    public async Task RecordMetricAsync(string resourceName, double latencyMs, bool success, CancellationToken ct = default)
    {
        PerformanceMetrics updatedMetrics = _metrics.AddOrUpdate(
            resourceName,
            // Add new
            _ => new PerformanceMetrics(
                resourceName,
                ExecutionCount: 1,
                AverageLatencyMs: latencyMs,
                SuccessRate: success ? 1.0 : 0.0,
                LastUsed: DateTime.UtcNow,
                CustomMetrics: new Dictionary<string, double>()),
            // Update existing
            (_, existing) =>
            {
                int newCount = existing.ExecutionCount + 1;
                double newAvgLatency = ((existing.AverageLatencyMs * existing.ExecutionCount) + latencyMs) / newCount;
                double newSuccessRate = ((existing.SuccessRate * existing.ExecutionCount) + (success ? 1.0 : 0.0)) / newCount;

                return new PerformanceMetrics(
                    resourceName,
                    ExecutionCount: newCount,
                    AverageLatencyMs: newAvgLatency,
                    SuccessRate: newSuccessRate,
                    LastUsed: DateTime.UtcNow,
                    CustomMetrics: existing.CustomMetrics);
            });

        // Persist to store if available
        if (_metricsStore != null)
        {
            await _metricsStore.StoreMetricsAsync(updatedMetrics, ct);
        }
    }

    /// <summary>
    /// Gets all current performance metrics.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        => new Dictionary<string, PerformanceMetrics>(_metrics);

    /// <summary>
    /// Gets the metrics store statistics if a persistent store is configured.
    /// </summary>
    public async Task<MetricsStoreStatistics?> GetMetricsStoreStatisticsAsync()
        => _metricsStore != null ? await _metricsStore.GetStatisticsAsync() : null;
}

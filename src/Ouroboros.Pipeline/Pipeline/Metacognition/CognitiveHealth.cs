using System.Runtime.CompilerServices;
using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents the overall cognitive health status of the system.
/// Aggregates metrics and alerts into a comprehensive health assessment.
/// </summary>
/// <param name="Timestamp">When this health assessment was generated.</param>
/// <param name="HealthScore">Overall health score from 0 (critical) to 1 (optimal).</param>
/// <param name="ProcessingEfficiency">Efficiency of cognitive processing from 0 to 1.</param>
/// <param name="ErrorRate">Rate of errors in recent processing window.</param>
/// <param name="ResponseLatency">Average response latency for cognitive operations.</param>
/// <param name="ActiveAlerts">Currently active monitoring alerts.</param>
/// <param name="Status">Overall health status classification.</param>
public sealed record CognitiveHealth(
    DateTime Timestamp,
    double HealthScore,
    double ProcessingEfficiency,
    double ErrorRate,
    TimeSpan ResponseLatency,
    ImmutableList<MonitoringAlert> ActiveAlerts,
    HealthStatus Status)
{
    /// <summary>
    /// Creates a healthy cognitive health status with optimal metrics.
    /// </summary>
    /// <returns>A healthy CognitiveHealth instance.</returns>
    public static CognitiveHealth Optimal() => new(
        Timestamp: DateTime.UtcNow,
        HealthScore: 1.0,
        ProcessingEfficiency: 1.0,
        ErrorRate: 0.0,
        ResponseLatency: TimeSpan.Zero,
        ActiveAlerts: ImmutableList<MonitoringAlert>.Empty,
        Status: HealthStatus.Healthy);

    /// <summary>
    /// Creates a CognitiveHealth from computed metrics.
    /// Automatically determines the status based on metrics.
    /// </summary>
    /// <param name="healthScore">The computed health score.</param>
    /// <param name="efficiency">The processing efficiency.</param>
    /// <param name="errorRate">The error rate.</param>
    /// <param name="latency">The response latency.</param>
    /// <param name="alerts">Active alerts.</param>
    /// <returns>A new CognitiveHealth with computed status.</returns>
    public static CognitiveHealth FromMetrics(
        double healthScore,
        double efficiency,
        double errorRate,
        TimeSpan latency,
        ImmutableList<MonitoringAlert> alerts)
    {
        var clampedHealth = Math.Clamp(healthScore, 0.0, 1.0);
        var clampedEfficiency = Math.Clamp(efficiency, 0.0, 1.0);
        var clampedErrorRate = Math.Max(0.0, errorRate);

        var status = DetermineStatus(clampedHealth, clampedEfficiency, clampedErrorRate, alerts);

        return new CognitiveHealth(
            Timestamp: DateTime.UtcNow,
            HealthScore: clampedHealth,
            ProcessingEfficiency: clampedEfficiency,
            ErrorRate: clampedErrorRate,
            ResponseLatency: latency,
            ActiveAlerts: alerts,
            Status: status);
    }

    /// <summary>
    /// Determines if the cognitive health requires attention.
    /// </summary>
    /// <returns>True if status is not Healthy.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RequiresAttention() => Status != HealthStatus.Healthy;

    /// <summary>
    /// Determines if the cognitive health is in a critical state.
    /// </summary>
    /// <returns>True if status is Critical.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCritical() => Status == HealthStatus.Critical;

    /// <summary>
    /// Validates the cognitive health values.
    /// </summary>
    /// <returns>A Result indicating validity or validation error.</returns>
    public Result<Unit, string> Validate()
    {
        if (HealthScore < 0.0 || HealthScore > 1.0)
        {
            return Result<Unit, string>.Failure($"HealthScore must be in [0, 1], got {HealthScore}.");
        }

        if (ProcessingEfficiency < 0.0 || ProcessingEfficiency > 1.0)
        {
            return Result<Unit, string>.Failure($"ProcessingEfficiency must be in [0, 1], got {ProcessingEfficiency}.");
        }

        if (ErrorRate < 0.0)
        {
            return Result<Unit, string>.Failure($"ErrorRate must be non-negative, got {ErrorRate}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }

    private static HealthStatus DetermineStatus(
        double healthScore,
        double efficiency,
        double errorRate,
        ImmutableList<MonitoringAlert> alerts)
    {
        var hasCriticalAlert = alerts.Any(a => a.Priority >= 9);
        var hasHighPriorityAlerts = alerts.Count(a => a.Priority >= 7) >= 2;

        if (hasCriticalAlert || healthScore < 0.3 || errorRate > 0.5)
        {
            return HealthStatus.Critical;
        }

        if (hasHighPriorityAlerts || healthScore < 0.5 || efficiency < 0.4 || errorRate > 0.3)
        {
            return HealthStatus.Impaired;
        }

        if (healthScore < 0.7 || efficiency < 0.6 || errorRate > 0.1 || alerts.Any())
        {
            return HealthStatus.Degraded;
        }

        return HealthStatus.Healthy;
    }
}
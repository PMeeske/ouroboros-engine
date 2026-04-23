namespace Ouroboros.Providers;

/// <summary>
/// Persists cost audit entries for cross-session analysis and reporting.
/// </summary>
public interface ICostRepository
{
    /// <summary>
    /// Save a cost audit entry.
    /// </summary>
    Task SaveAsync(CostAuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get entries for a specific session.
    /// </summary>
    Task<IReadOnlyList<CostAuditEntry>> GetBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get entries within a date range.
    /// </summary>
    Task<IReadOnlyList<CostAuditEntry>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get aggregated daily costs.
    /// </summary>
    Task<IReadOnlyList<DailyCostAggregate>> GetDailyAggregatesAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single cost audit record.
/// </summary>
public sealed record CostAuditEntry(
    Guid Id,
    Guid SessionId,
    string Provider,
    string Model,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    string TaskType,
    DateTime Timestamp);

/// <summary>
/// Aggregated cost data for a single day.
/// </summary>
public sealed record DailyCostAggregate(
    DateTime Date,
    int RequestCount,
    long TotalTokens,
    decimal TotalCostUsd,
    string Provider);

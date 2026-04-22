namespace Ouroboros.Providers;

/// <summary>
/// In-memory implementation of <see cref="ICostRepository"/> for development and testing.
/// </summary>
public class InMemoryCostRepository : ICostRepository
{
    private readonly List<CostAuditEntry> _entries = new();
    private readonly object _lock = new();

    /// <inheritdoc/>
    public Task SaveAsync(CostAuditEntry entry, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _entries.Add(entry);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<CostAuditEntry>> GetBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var result = _entries.Where(e => e.SessionId == sessionId).ToList();
            return Task.FromResult<IReadOnlyList<CostAuditEntry>>(result);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<CostAuditEntry>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var result = _entries.Where(e => e.Timestamp >= from && e.Timestamp <= to).ToList();
            return Task.FromResult<IReadOnlyList<CostAuditEntry>>(result);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<DailyCostAggregate>> GetDailyAggregatesAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var result = _entries
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .GroupBy(e => new { e.Timestamp.Date, e.Provider })
                .Select(g => new DailyCostAggregate(
                    g.Key.Date,
                    g.Count(),
                    g.Sum(e => (long)e.InputTokens + e.OutputTokens),
                    g.Sum(e => e.CostUsd),
                    g.Key.Provider))
                .ToList();

            return Task.FromResult<IReadOnlyList<DailyCostAggregate>>(result);
        }
    }
}

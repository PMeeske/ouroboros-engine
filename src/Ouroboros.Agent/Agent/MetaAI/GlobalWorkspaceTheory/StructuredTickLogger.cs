// ==========================================================
// Global Workspace Theory — Conscious Access Report
// Plan 4: Structured tick logger for downstream analysis
// ==========================================================

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Logs tick reports both to an in-memory ring buffer and to an <see cref="ILogger"/>.
/// </summary>
public sealed class StructuredTickLogger : ITickLogger
{
    private readonly ConcurrentQueue<ConsciousAccessReport> _reports = new();
    private readonly ILogger<StructuredTickLogger>? _logger;
    private readonly int _maxBufferedReports;

    /// <summary>
    /// Creates a new structured tick logger.
    /// </summary>
    /// <param name="logger">Optional Microsoft.Extensions.Logging logger</param>
    /// <param name="maxBufferedReports">Maximum reports to keep in memory; default 1000</param>
    public StructuredTickLogger(ILogger<StructuredTickLogger>? logger = null, int maxBufferedReports = 1000)
    {
        _logger = logger;
        _maxBufferedReports = maxBufferedReports;
    }

    /// <summary>
    /// All buffered reports, oldest first.
    /// </summary>
    public IReadOnlyList<ConsciousAccessReport> BufferedReports => _reports.ToList();

    /// <inheritdoc/>
    public Task LogAsync(ConsciousAccessReport report, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        _reports.Enqueue(report);

        while (_reports.Count > _maxBufferedReports && _reports.TryDequeue(out _))
        {
        }

        _logger?.LogInformation(
            "Tick #{TickNumber}: {Summary} | Entropy: {Entropy:F2} | Duration: {DurationMs:F1}ms",
            report.TickNumber,
            report.Summary,
            report.Entropy,
            report.DurationMs);

        return Task.CompletedTask;
    }
}

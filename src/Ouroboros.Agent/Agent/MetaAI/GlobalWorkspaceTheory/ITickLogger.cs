// ==========================================================
// Global Workspace Theory — Conscious Access Report
// Plan 4: ITickLogger interface
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Logs conscious access reports for downstream analysis.
/// </summary>
public interface ITickLogger
{
    /// <summary>
    /// Logs a conscious access report.
    /// </summary>
    /// <param name="report">The report to log</param>
    /// <param name="ct">Cancellation token</param>
    Task LogAsync(ConsciousAccessReport report, CancellationToken ct = default);
}

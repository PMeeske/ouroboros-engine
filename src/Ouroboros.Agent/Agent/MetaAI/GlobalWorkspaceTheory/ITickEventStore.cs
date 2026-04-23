// ==========================================================
// Global Workspace Theory — Cognitive Tick Loop
// Plan 5: ITickEventStore interface for persistence
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Persists cognitive tick events for replay and analysis.
/// </summary>
public interface ITickEventStore
{
    /// <summary>
    /// Appends a tick event to the store.
    /// </summary>
    /// <param name="tickEvent">The tick to persist</param>
    /// <param name="ct">Cancellation token</param>
    Task AppendAsync(TickEvent tickEvent, CancellationToken ct = default);

    /// <summary>
    /// Reads tick events within a range.
    /// </summary>
    /// <param name="fromTick">Inclusive start tick number</param>
    /// <param name="toTick">Inclusive end tick number</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tick events in ascending order</returns>
    Task<IReadOnlyList<TickEvent>> ReadRangeAsync(long fromTick, long toTick, CancellationToken ct = default);
}

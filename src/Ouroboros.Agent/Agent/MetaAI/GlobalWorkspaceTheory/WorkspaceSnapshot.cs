// ==========================================================
// Global Workspace Theory — Capacity-Limited Workspace
// Plan 2: WorkspaceSnapshot for introspection
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Immutable snapshot of the global workspace at a point in time.
/// </summary>
public sealed record WorkspaceSnapshot(
    IReadOnlyList<WorkspaceChunk> Chunks,
    int Capacity,
    DateTime CapturedAt)
{
    /// <summary>
    /// Number of chunks currently in the workspace.
    /// </summary>
    public int Count => Chunks.Count;

    /// <summary>
    /// Whether the workspace is at full capacity.
    /// </summary>
    public bool IsFull => Count >= Capacity;

    /// <summary>
    /// Number of free slots remaining.
    /// </summary>
    public int FreeSlots => Math.Max(0, Capacity - Count);
}

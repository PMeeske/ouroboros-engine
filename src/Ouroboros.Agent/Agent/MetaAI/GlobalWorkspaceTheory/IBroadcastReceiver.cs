// ==========================================================
// Global Workspace Theory — Global Broadcast
// Plan 3: IBroadcastReceiver interface
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Implemented by subsystems that receive global workspace broadcasts.
/// </summary>
public interface IBroadcastReceiver
{
    /// <summary>
    /// Unique name of the receiving subsystem.
    /// </summary>
    string ReceiverName { get; }

    /// <summary>
    /// Called when the workspace broadcasts its current contents.
    /// </summary>
    /// <param name="chunks">Current workspace chunks</param>
    /// <param name="ct">Cancellation token</param>
    Task OnBroadcastAsync(IReadOnlyList<WorkspaceChunk> chunks, CancellationToken ct = default);
}

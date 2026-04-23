// ==========================================================
// Global Workspace Theory — Global Broadcast
// Plan 3: BroadcastBus (pub/sub over workspace contents)
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Pub/sub bus that broadcasts workspace contents to all registered receivers.
/// </summary>
public sealed class BroadcastBus
{
    private readonly ConcurrentDictionary<string, IBroadcastReceiver> _receivers = new();

    /// <summary>
    /// Registers a broadcast receiver.
    /// </summary>
    /// <param name="receiver">The receiver to register</param>
    /// <returns>True if registered; false if a receiver with the same name already exists</returns>
    public bool Register(IBroadcastReceiver receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return _receivers.TryAdd(receiver.ReceiverName, receiver);
    }

    /// <summary>
    /// Unregisters a broadcast receiver by name.
    /// </summary>
    /// <param name="receiverName">Name of the receiver to unregister</param>
    /// <returns>True if removed, false if not found</returns>
    public bool Unregister(string receiverName)
    {
        ArgumentNullException.ThrowIfNull(receiverName);
        return _receivers.TryRemove(receiverName, out _);
    }

    /// <summary>
    /// Number of registered receivers.
    /// </summary>
    public int ReceiverCount => _receivers.Count;

    /// <summary>
    /// Broadcasts current workspace contents to all registered receivers sequentially.
    /// </summary>
    /// <param name="chunks">Current workspace chunks</param>
    /// <param name="ct">Cancellation token</param>
    public async Task BroadcastAsync(IReadOnlyList<WorkspaceChunk> chunks, CancellationToken ct = default)
    {
        foreach (IBroadcastReceiver receiver in _receivers.Values)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await receiver.OnBroadcastAsync(chunks, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031
            catch (Exception)
            {
                // Isolated failure — other receivers must still get the broadcast
            }
#pragma warning restore CA1031
        }
    }
}

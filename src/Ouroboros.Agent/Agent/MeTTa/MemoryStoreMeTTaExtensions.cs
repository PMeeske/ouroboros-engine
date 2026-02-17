using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tools.MeTTa;

/// <summary>
/// Extension methods for MemoryStore to integrate with MeTTa.
/// </summary>
public static class MemoryStoreMeTTaExtensions
{
    /// <summary>
    /// Creates a MeTTa bridge for this memory store.
    /// </summary>
    /// <param name="memory">The memory store.</param>
    /// <param name="engine">The MeTTa engine to bridge to.</param>
    /// <returns>A configured MeTTa memory bridge.</returns>
    public static MeTTaMemoryBridge CreateMeTTaBridge(this MemoryStore memory, IMeTTaEngine engine)
    {
        return new MeTTaMemoryBridge(engine, memory);
    }

    /// <summary>
    /// Synchronizes memory to a MeTTa engine.
    /// </summary>
    /// <param name="memory">The memory store.</param>
    /// <param name="engine">The MeTTa engine.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of facts synchronized.</returns>
    public static async Task<Result<int, string>> SyncToMeTTaAsync(
        this MemoryStore memory,
        IMeTTaEngine engine,
        CancellationToken ct = default)
    {
        MeTTaMemoryBridge bridge = memory.CreateMeTTaBridge(engine);
        return await bridge.SyncAllExperiencesAsync(ct);
    }
}
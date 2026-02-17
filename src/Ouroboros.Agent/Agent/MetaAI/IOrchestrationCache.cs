namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for orchestration decision caching.
/// </summary>
public interface IOrchestrationCache
{
    /// <summary>
    /// Gets a cached orchestration decision for the given prompt hash.
    /// </summary>
    /// <param name="promptHash">Hash of the prompt.</param>
    /// <returns>Cached decision if found, None otherwise.</returns>
    Task<Option<OrchestratorDecision>> GetCachedDecisionAsync(string promptHash);

    /// <summary>
    /// Caches an orchestration decision.
    /// </summary>
    /// <param name="promptHash">Hash of the prompt.</param>
    /// <param name="decision">The decision to cache.</param>
    /// <param name="ttl">Time to live for the cache entry.</param>
    Task CacheDecisionAsync(string promptHash, OrchestratorDecision decision, TimeSpan ttl);

    /// <summary>
    /// Invalidates a specific cache entry.
    /// </summary>
    /// <param name="promptHash">Hash of the prompt to invalidate.</param>
    Task InvalidateAsync(string promptHash);

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    CacheStatistics GetStatistics();
}
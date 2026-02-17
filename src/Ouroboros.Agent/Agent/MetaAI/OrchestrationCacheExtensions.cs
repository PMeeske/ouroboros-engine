namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Extension methods for orchestrator caching.
/// </summary>
public static class OrchestrationCacheExtensions
{
    /// <summary>
    /// Default TTL for cached decisions (5 minutes).
    /// </summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Wraps an orchestrator with caching capability.
    /// </summary>
    public static CachingModelOrchestrator WithCaching(
        this IModelOrchestrator orchestrator,
        IOrchestrationCache cache,
        TimeSpan? ttl = null)
    {
        return new CachingModelOrchestrator(orchestrator, cache, ttl ?? DefaultTtl);
    }
}
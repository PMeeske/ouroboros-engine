namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Decorator that adds caching to an existing orchestrator.
/// </summary>
public sealed class CachingModelOrchestrator : IModelOrchestrator
{
    private readonly IModelOrchestrator _inner;
    private readonly IOrchestrationCache _cache;
    private readonly TimeSpan _ttl;

    /// <summary>
    /// Creates a new caching orchestrator wrapper.
    /// </summary>
    public CachingModelOrchestrator(
        IModelOrchestrator inner,
        IOrchestrationCache cache,
        TimeSpan ttl)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _ttl = ttl;
    }

    /// <inheritdoc/>
    public async Task<Result<OrchestratorDecision, string>> SelectModelAsync(
        string prompt,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        // Generate cache key
        var hash = InMemoryOrchestrationCache.GeneratePromptHash(prompt, context);

        // Check cache first
        var cached = await _cache.GetCachedDecisionAsync(hash);
        if (cached.HasValue)
        {
            return Result<OrchestratorDecision, string>.Success(cached.Value!);
        }

        // Cache miss - call inner orchestrator
        var result = await _inner.SelectModelAsync(prompt, context, ct);

        // Cache successful results
        if (result.IsSuccess)
        {
            await _cache.CacheDecisionAsync(hash, result.Value, _ttl);
        }

        return result;
    }

    /// <inheritdoc/>
    public UseCase ClassifyUseCase(string prompt) => _inner.ClassifyUseCase(prompt);

    /// <inheritdoc/>
    public void RegisterModel(ModelCapability capability) => _inner.RegisterModel(capability);

    /// <inheritdoc/>
    public void RecordMetric(string resourceName, double latencyMs, bool success) =>
        _inner.RecordMetric(resourceName, latencyMs, success);

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics() => _inner.GetMetrics();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public CacheStatistics GetCacheStatistics() => _cache.GetStatistics();

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public Task ClearCacheAsync() => _cache.ClearAsync();
}
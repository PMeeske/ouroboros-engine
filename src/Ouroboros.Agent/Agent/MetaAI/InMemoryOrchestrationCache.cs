#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Orchestration Cache
// Caching layer for orchestration decisions to improve performance
// ==========================================================

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// In-memory implementation of orchestration cache.
/// Thread-safe and suitable for single-instance deployments.
/// </summary>
public sealed class InMemoryOrchestrationCache : IOrchestrationCache, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _cleanupInterval;
    private readonly int _maxEntries;
    private long _hits;
    private long _misses;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the cache with default settings.
    /// </summary>
    public InMemoryOrchestrationCache()
        : this(maxEntries: 10000, cleanupIntervalSeconds: 60)
    {
    }

    /// <summary>
    /// Initializes a new instance of the cache with custom settings.
    /// </summary>
    /// <param name="maxEntries">Maximum number of cache entries.</param>
    /// <param name="cleanupIntervalSeconds">Interval for cleanup operations.</param>
    public InMemoryOrchestrationCache(int maxEntries, int cleanupIntervalSeconds = 60)
    {
        _maxEntries = maxEntries;
        _cleanupInterval = TimeSpan.FromSeconds(cleanupIntervalSeconds);
        _cleanupTimer = new Timer(
            _ => CleanupExpiredEntries(),
            null,
            _cleanupInterval,
            _cleanupInterval);
    }

    /// <inheritdoc/>
    public Task<Option<OrchestratorDecision>> GetCachedDecisionAsync(string promptHash)
    {
        if (string.IsNullOrEmpty(promptHash))
        {
            Interlocked.Increment(ref _misses);
            return Task.FromResult(Option<OrchestratorDecision>.None());
        }

        if (_cache.TryGetValue(promptHash, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                entry.IncrementAccessCount();
                Interlocked.Increment(ref _hits);
                return Task.FromResult(Option<OrchestratorDecision>.Some(entry.Decision));
            }
            else
            {
                // Entry expired, remove it
                _cache.TryRemove(promptHash, out _);
            }
        }

        Interlocked.Increment(ref _misses);
        return Task.FromResult(Option<OrchestratorDecision>.None());
    }

    /// <inheritdoc/>
    public Task CacheDecisionAsync(string promptHash, OrchestratorDecision decision, TimeSpan ttl)
    {
        if (string.IsNullOrEmpty(promptHash) || decision == null)
        {
            return Task.CompletedTask;
        }

        // Check if we need to evict entries
        if (_cache.Count >= _maxEntries)
        {
            EvictLeastRecentlyUsed();
        }

        var entry = new CacheEntry(decision, DateTime.UtcNow.Add(ttl));
        _cache.AddOrUpdate(promptHash, entry, (_, _) => entry);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task InvalidateAsync(string promptHash)
    {
        if (!string.IsNullOrEmpty(promptHash))
        {
            _cache.TryRemove(promptHash, out _);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAsync()
    {
        _cache.Clear();
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public CacheStatistics GetStatistics()
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);
        var total = hits + misses;

        return new CacheStatistics(
            TotalEntries: _cache.Count,
            MaxEntries: _maxEntries,
            HitCount: hits,
            MissCount: misses,
            HitRate: total > 0 ? (double)hits / total : 0,
            MemoryEstimateBytes: EstimateMemoryUsage());
    }

    /// <summary>
    /// Generates a hash for a prompt.
    /// </summary>
    public static string GeneratePromptHash(string prompt, Dictionary<string, object>? context = null)
    {
        var sb = new StringBuilder(prompt);

        if (context != null)
        {
            foreach (var kvp in context.OrderBy(k => k.Key))
            {
                sb.Append($"|{kvp.Key}:{kvp.Value}");
            }
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer.Dispose();
            _cache.Clear();
            _disposed = true;
        }
    }

    private void CleanupExpiredEntries()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }

    private void EvictLeastRecentlyUsed()
    {
        // Evict 10% of entries based on last access time
        var toEvict = _cache
            .OrderBy(kvp => kvp.Value.LastAccessedAt)
            .Take(_maxEntries / 10)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toEvict)
        {
            _cache.TryRemove(key, out _);
        }
    }

    private long EstimateMemoryUsage()
    {
        // Rough estimate: ~500 bytes per entry on average
        return _cache.Count * 500L;
    }

    private sealed class CacheEntry
    {
        private int _accessCount;

        public OrchestratorDecision Decision { get; }
        public DateTime ExpiresAt { get; }
        public DateTime LastAccessedAt { get; private set; }
        public int AccessCount => _accessCount;

        public CacheEntry(OrchestratorDecision decision, DateTime expiresAt)
        {
            Decision = decision;
            ExpiresAt = expiresAt;
            LastAccessedAt = DateTime.UtcNow;
            _accessCount = 1;
        }

        public void IncrementAccessCount()
        {
            LastAccessedAt = DateTime.UtcNow;
            Interlocked.Increment(ref _accessCount);
        }
    }
}
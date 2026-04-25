using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class InMemoryOrchestrationCacheTests : IDisposable
{
    private readonly InMemoryOrchestrationCache _cache;

    public InMemoryOrchestrationCacheTests()
    {
        _cache = new InMemoryOrchestrationCache();
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    #region Constructors

    [Fact]
    public void Constructor_Default_ShouldInitialize()
    {
        var cache = new InMemoryOrchestrationCache();
        cache.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomSettings_ShouldSetMaxEntries()
    {
        var cache = new InMemoryOrchestrationCache(500, 30);
        cache.Should().NotBeNull();
    }

    #endregion

    #region CacheDecisionAsync

    [Fact]
    public async Task CacheDecisionAsync_ValidEntry_ShouldStore()
    {
        var decision = new OrchestratorDecision("model-1", UseCaseType.General, 0.9, new Dictionary<string, object>());
        await _cache.CacheDecisionAsync("hash1", decision, TimeSpan.FromMinutes(5));

        var result = await _cache.GetCachedDecisionAsync("hash1");
        result.IsSome.Should().BeTrue();
    }

    [Fact]
    public async Task CacheDecisionAsync_NullPromptHash_ShouldNotStore()
    {
        var decision = new OrchestratorDecision("model-1", UseCaseType.General, 0.9, new Dictionary<string, object>());
        await _cache.CacheDecisionAsync(null!, decision, TimeSpan.FromMinutes(5));

        var result = await _cache.GetCachedDecisionAsync(null!);
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public async Task CacheDecisionAsync_EmptyPromptHash_ShouldNotStore()
    {
        var decision = new OrchestratorDecision("model-1", UseCaseType.General, 0.9, new Dictionary<string, object>());
        await _cache.CacheDecisionAsync("", decision, TimeSpan.FromMinutes(5));

        var result = await _cache.GetCachedDecisionAsync("");
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public async Task CacheDecisionAsync_NullDecision_ShouldNotStore()
    {
        await _cache.CacheDecisionAsync("hash1", null!, TimeSpan.FromMinutes(5));

        var result = await _cache.GetCachedDecisionAsync("hash1");
        result.IsSome.Should().BeFalse();
    }

    #endregion

    #region GetCachedDecisionAsync

    [Fact]
    public async Task GetCachedDecisionAsync_ExistingEntry_ShouldReturnSome()
    {
        var decision = new OrchestratorDecision("model-1", UseCaseType.General, 0.9, new Dictionary<string, object>());
        await _cache.CacheDecisionAsync("hash1", decision, TimeSpan.FromMinutes(5));

        var result = await _cache.GetCachedDecisionAsync("hash1");
        result.IsSome.Should().BeTrue();
        result.Value.ModelId.Should().Be("model-1");
    }

    [Fact]
    public async Task GetCachedDecisionAsync_MissingEntry_ShouldReturnNone()
    {
        var result = await _cache.GetCachedDecisionAsync("nonexistent");
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public async Task GetCachedDecisionAsync_NullPromptHash_ShouldReturnNone()
    {
        var result = await _cache.GetCachedDecisionAsync(null!);
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public async Task GetCachedDecisionAsync_ExpiredEntry_ShouldReturnNone()
    {
        var decision = new OrchestratorDecision("model-1", UseCaseType.General, 0.9, new Dictionary<string, object>());
        await _cache.CacheDecisionAsync("hash1", decision, TimeSpan.FromMilliseconds(1));

        await Task.Delay(50);

        var result = await _cache.GetCachedDecisionAsync("hash1");
        result.IsSome.Should().BeFalse();
    }

    #endregion

    #region InvalidateAsync

    [Fact]
    public async Task InvalidateAsync_ExistingEntry_ShouldRemove()
    {
        var decision = new OrchestratorDecision("model-1", UseCaseType.General, 0.9, new Dictionary<string, object>());
        await _cache.CacheDecisionAsync("hash1", decision, TimeSpan.FromMinutes(5));

        await _cache.InvalidateAsync("hash1");

        var result = await _cache.GetCachedDecisionAsync("hash1");
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateAsync_NullPromptHash_ShouldNotThrow()
    {
        await _cache.InvalidateAsync(null!);
    }

    [Fact]
    public async Task InvalidateAsync_NonExistingEntry_ShouldNotThrow()
    {
        await _cache.InvalidateAsync("nonexistent");
    }

    #endregion

    #region ClearAsync

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllEntries()
    {
        var decision = new OrchestratorDecision("model-1", UseCaseType.General, 0.9, new Dictionary<string, object>());
        await _cache.CacheDecisionAsync("hash1", decision, TimeSpan.FromMinutes(5));
        await _cache.CacheDecisionAsync("hash2", decision, TimeSpan.FromMinutes(5));

        await _cache.ClearAsync();

        var result1 = await _cache.GetCachedDecisionAsync("hash1");
        var result2 = await _cache.GetCachedDecisionAsync("hash2");
        result1.IsSome.Should().BeFalse();
        result2.IsSome.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_ShouldResetStatistics()
    {
        var decision = new OrchestratorDecision("model-1", UseCaseType.General, 0.9, new Dictionary<string, object>());
        await _cache.CacheDecisionAsync("hash1", decision, TimeSpan.FromMinutes(5));
        await _cache.GetCachedDecisionAsync("hash1");

        await _cache.ClearAsync();

        var stats = _cache.GetStatistics();
        stats.HitCount.Should().Be(0);
        stats.MissCount.Should().Be(0);
    }

    #endregion

    #region GetStatistics

    [Fact]
    public void GetStatistics_EmptyCache_ShouldReturnZeroStats()
    {
        var stats = _cache.GetStatistics();
        stats.TotalEntries.Should().Be(0);
        stats.HitCount.Should().Be(0);
        stats.MissCount.Should().Be(0);
        stats.HitRate.Should().Be(0);
    }

    [Fact]
    public async Task GetStatistics_AfterHitsAndMisses_ShouldCalculateHitRate()
    {
        var decision = new OrchestratorDecision("model-1", UseCaseType.General, 0.9, new Dictionary<string, object>());
        await _cache.CacheDecisionAsync("hash1", decision, TimeSpan.FromMinutes(5));

        await _cache.GetCachedDecisionAsync("hash1"); // hit
        await _cache.GetCachedDecisionAsync("hash1"); // hit
        await _cache.GetCachedDecisionAsync("missing"); // miss

        var stats = _cache.GetStatistics();
        stats.HitCount.Should().Be(2);
        stats.MissCount.Should().Be(1);
        stats.HitRate.Should().BeApproximately(0.6667, 0.001);
    }

    #endregion

    #region GeneratePromptHash

    [Fact]
    public void GeneratePromptHash_WithPromptOnly_ShouldReturnHash()
    {
        var hash = InMemoryOrchestrationCache.GeneratePromptHash("test prompt");
        hash.Should().NotBeNullOrEmpty();
        hash.Length.Should().Be(64); // SHA256 hex length
    }

    [Fact]
    public void GeneratePromptHash_WithContext_ShouldIncludeContext()
    {
        var hash1 = InMemoryOrchestrationCache.GeneratePromptHash("prompt", new Dictionary<string, object> { ["key"] = "value" });
        var hash2 = InMemoryOrchestrationCache.GeneratePromptHash("prompt", new Dictionary<string, object> { ["key"] = "other" });

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void GeneratePromptHash_SameInput_ShouldReturnSameHash()
    {
        var hash1 = InMemoryOrchestrationCache.GeneratePromptHash("prompt", new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 });
        var hash2 = InMemoryOrchestrationCache.GeneratePromptHash("prompt", new Dictionary<string, object> { ["b"] = 2, ["a"] = 1 });

        hash1.Should().Be(hash2);
    }

    #endregion

    #region Eviction

    [Fact]
    public async Task CacheDecisionAsync_MaxEntriesReached_ShouldEvict()
    {
        var smallCache = new InMemoryOrchestrationCache(10);
        var decision = new OrchestratorDecision("model-1", UseCaseType.General, 0.9, new Dictionary<string, object>());

        for (int i = 0; i < 15; i++)
        {
            await smallCache.CacheDecisionAsync($"hash{i}", decision, TimeSpan.FromMinutes(5));
        }

        var stats = smallCache.GetStatistics();
        stats.TotalEntries.Should().BeLessThanOrEqualTo(10);

        smallCache.Dispose();
    }

    #endregion
}

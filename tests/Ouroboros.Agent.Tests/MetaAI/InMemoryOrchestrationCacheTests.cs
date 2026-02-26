// <copyright file="InMemoryOrchestrationCacheTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class InMemoryOrchestrationCacheTests : IDisposable
{
    private readonly InMemoryOrchestrationCache _cache = new(maxEntries: 100, cleanupIntervalSeconds: 3600);

    [Fact]
    public void Constructor_DefaultParams_DoesNotThrow()
    {
        using var cache = new InMemoryOrchestrationCache();
        cache.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_CustomParams_DoesNotThrow()
    {
        using var cache = new InMemoryOrchestrationCache(maxEntries: 500, cleanupIntervalSeconds: 120);
        cache.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCachedDecisionAsync_EmptyHash_ReturnsMiss()
    {
        var result = await _cache.GetCachedDecisionAsync("");
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task GetCachedDecisionAsync_NullHash_ReturnsMiss()
    {
        var result = await _cache.GetCachedDecisionAsync(null!);
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task GetCachedDecisionAsync_NonexistentKey_ReturnsMiss()
    {
        var result = await _cache.GetCachedDecisionAsync("nonexistent-hash");
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task CacheDecisionAsync_ThenGet_ReturnsHit()
    {
        var decision = CreateDecision();
        await _cache.CacheDecisionAsync("test-hash", decision, TimeSpan.FromMinutes(10));

        var result = await _cache.GetCachedDecisionAsync("test-hash");

        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public async Task CacheDecisionAsync_NullHash_DoesNotThrow()
    {
        var decision = CreateDecision();
        await _cache.CacheDecisionAsync(null!, decision, TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task CacheDecisionAsync_NullDecision_DoesNotThrow()
    {
        await _cache.CacheDecisionAsync("hash", null!, TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task InvalidateAsync_RemovesEntry()
    {
        var decision = CreateDecision();
        await _cache.CacheDecisionAsync("to-remove", decision, TimeSpan.FromMinutes(10));
        await _cache.InvalidateAsync("to-remove");

        var result = await _cache.GetCachedDecisionAsync("to-remove");
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateAsync_EmptyHash_DoesNotThrow()
    {
        await _cache.InvalidateAsync("");
    }

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
        var decision = CreateDecision();
        await _cache.CacheDecisionAsync("k1", decision, TimeSpan.FromMinutes(10));
        await _cache.CacheDecisionAsync("k2", decision, TimeSpan.FromMinutes(10));

        await _cache.ClearAsync();

        var stats = _cache.GetStatistics();
        stats.TotalEntries.Should().Be(0);
    }

    [Fact]
    public void GetStatistics_InitiallyEmpty()
    {
        var stats = _cache.GetStatistics();

        stats.TotalEntries.Should().Be(0);
        stats.MaxEntries.Should().Be(100);
        stats.HitCount.Should().Be(0);
        stats.MissCount.Should().Be(0);
    }

    [Fact]
    public async Task GetStatistics_TracksHitsAndMisses()
    {
        var decision = CreateDecision();
        await _cache.CacheDecisionAsync("hit-key", decision, TimeSpan.FromMinutes(10));

        await _cache.GetCachedDecisionAsync("hit-key");   // hit
        await _cache.GetCachedDecisionAsync("miss-key");  // miss

        var stats = _cache.GetStatistics();

        stats.HitCount.Should().Be(1);
        stats.MissCount.Should().Be(1);
        stats.HitRate.Should().Be(0.5);
    }

    [Fact]
    public void GeneratePromptHash_SameInput_SameHash()
    {
        var hash1 = InMemoryOrchestrationCache.GeneratePromptHash("test prompt");
        var hash2 = InMemoryOrchestrationCache.GeneratePromptHash("test prompt");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GeneratePromptHash_DifferentInput_DifferentHash()
    {
        var hash1 = InMemoryOrchestrationCache.GeneratePromptHash("prompt A");
        var hash2 = InMemoryOrchestrationCache.GeneratePromptHash("prompt B");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void GeneratePromptHash_WithContext_DiffersFromWithout()
    {
        var hashWithout = InMemoryOrchestrationCache.GeneratePromptHash("test");
        var hashWith = InMemoryOrchestrationCache.GeneratePromptHash(
            "test",
            new Dictionary<string, object> { ["key"] = "value" });

        hashWithout.Should().NotBe(hashWith);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var cache = new InMemoryOrchestrationCache();
        cache.Dispose();
        var act = () => cache.Dispose();
        act.Should().NotThrow();
    }

    private static OrchestratorDecision CreateDecision()
    {
        var llmMock = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        return new OrchestratorDecision(
            llmMock.Object,
            "model-1",
            "Selected for test",
            new ToolRegistry(),
            0.9);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}

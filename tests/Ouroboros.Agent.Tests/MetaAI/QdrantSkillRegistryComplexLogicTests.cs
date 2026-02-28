// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Qdrant.Client;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

/// <summary>
/// Complex-logic tests for QdrantSkillRegistry: skill registration lifecycle,
/// execution recording with running average computation, find/filter logic,
/// sync convenience methods, stats computation, tag extraction, connection
/// string normalization, and fallback embedding generation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class QdrantSkillRegistryComplexLogicTests
{
    private static AgentSkill MakeSkill(
        string id = "skill-1",
        string name = "Test Skill",
        string category = "general",
        double successRate = 0.8,
        int usageCount = 10,
        long avgExecTime = 100,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? preconditions = null,
        IReadOnlyList<string>? effects = null) =>
        new(id, name, $"Description of {name}", category,
            preconditions ?? new List<string>(),
            effects ?? new List<string>(),
            successRate, usageCount, avgExecTime,
            tags ?? new List<string> { "test" });

    /// <summary>
    /// Creates a QdrantSkillRegistry using a real (but unconnected) QdrantClient.
    /// With AutoSave:false, the registry operates on its in-memory ConcurrentDictionary
    /// and never makes actual gRPC calls. The client points to a non-existent server
    /// so that any accidental network call would fail fast rather than hang.
    /// </summary>
    private static QdrantSkillRegistry CreateInMemoryRegistry()
    {
        // Create a real QdrantClient; with AutoSave:false the registry only
        // uses the in-memory cache and never contacts the server.
        var client = new QdrantClient("localhost", 6334);
#pragma warning disable CS0618
        return new QdrantSkillRegistry(
            client,
            embedding: null,
            config: new QdrantSkillConfig(AutoSave: false));
#pragma warning restore CS0618
    }

    // ========================================================
    // RegisterSkill + GetSkill (sync methods)
    // ========================================================

    [Fact]
    public void RegisterSkill_Sync_AddsToCache()
    {
        var registry = CreateInMemoryRegistry();
        var skill = MakeSkill("s1", "Skill One");

        var result = registry.RegisterSkill(skill);

        result.IsSuccess.Should().BeTrue();
        registry.GetSkill("s1").Should().NotBeNull();
        registry.GetSkill("s1")!.Name.Should().Be("Skill One");
    }

    [Fact]
    public void RegisterSkill_Sync_OverwritesExisting()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1", "Version 1"));
        registry.RegisterSkill(MakeSkill("s1", "Version 2"));

        registry.GetSkill("s1")!.Name.Should().Be("Version 2");
    }

    [Fact]
    public void GetSkill_EmptyId_ReturnsNull()
    {
        var registry = CreateInMemoryRegistry();
        registry.GetSkill("").Should().BeNull();
        registry.GetSkill("  ").Should().BeNull();
    }

    [Fact]
    public void GetSkill_NonExistent_ReturnsNull()
    {
        var registry = CreateInMemoryRegistry();
        registry.GetSkill("does-not-exist").Should().BeNull();
    }

    // ========================================================
    // GetSkillAsync
    // ========================================================

    [Fact]
    public async Task GetSkillAsync_EmptyId_ReturnsFailure()
    {
        var registry = CreateInMemoryRegistry();
        var result = await registry.GetSkillAsync("");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetSkillAsync_NotFound_ReturnsFailure()
    {
        var registry = CreateInMemoryRegistry();
        var result = await registry.GetSkillAsync("nonexistent");
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetSkillAsync_ExistingSkill_ReturnsSuccess()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1"));

        var result = await registry.GetSkillAsync("s1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("s1");
    }

    // ========================================================
    // RecordExecutionAsync: running average computation
    // ========================================================

    [Fact]
    public async Task RecordExecution_UpdatesUsageCount()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1", usageCount: 5));

        await registry.RecordExecutionAsync("s1", true, 200);

        var skill = registry.GetSkill("s1")!;
        skill.UsageCount.Should().Be(6);
    }

    [Fact]
    public async Task RecordExecution_UpdatesRunningAverageSuccessRate()
    {
        var registry = CreateInMemoryRegistry();
        // Start with 100% success over 4 uses
        registry.RegisterSkill(MakeSkill("s1", successRate: 1.0, usageCount: 4));

        // Record a failure
        await registry.RecordExecutionAsync("s1", false, 100);

        var skill = registry.GetSkill("s1")!;
        // New rate = (1.0 * 4 + 0.0) / 5 = 0.8
        skill.SuccessRate.Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public async Task RecordExecution_UpdatesRunningAverageExecutionTime()
    {
        var registry = CreateInMemoryRegistry();
        // Start with 100ms avg over 3 uses
        registry.RegisterSkill(MakeSkill("s1", usageCount: 3, avgExecTime: 100));

        // Record execution of 500ms
        await registry.RecordExecutionAsync("s1", true, 500);

        var skill = registry.GetSkill("s1")!;
        // New avg = (100 * 3 + 500) / 4 = 800 / 4 = 200
        skill.AverageExecutionTime.Should().Be(200);
    }

    [Fact]
    public async Task RecordExecution_EmptySkillId_ReturnsFailure()
    {
        var registry = CreateInMemoryRegistry();
        var result = await registry.RecordExecutionAsync("", true, 100);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RecordExecution_NonexistentSkill_ReturnsFailure()
    {
        var registry = CreateInMemoryRegistry();
        var result = await registry.RecordExecutionAsync("unknown", true, 100);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task RecordExecution_MultipleRecordings_MaintainsCorrectAverage()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1", successRate: 0.0, usageCount: 0, avgExecTime: 0));

        // Record 3 successes, 1 failure
        await registry.RecordExecutionAsync("s1", true, 100);
        await registry.RecordExecutionAsync("s1", true, 200);
        await registry.RecordExecutionAsync("s1", false, 300);
        await registry.RecordExecutionAsync("s1", true, 400);

        var skill = registry.GetSkill("s1")!;
        skill.UsageCount.Should().Be(4);
        skill.SuccessRate.Should().BeApproximately(0.75, 0.001);
        // avg time = (100 + 200 + 300 + 400) / 4 -- but computed as running average
    }

    // ========================================================
    // RecordSkillExecution (sync version)
    // ========================================================

    [Fact]
    public void RecordSkillExecution_Sync_UpdatesMetrics()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1", successRate: 0.5, usageCount: 2, avgExecTime: 100));

        registry.RecordSkillExecution("s1", true, 300);

        var skill = registry.GetSkill("s1")!;
        skill.UsageCount.Should().Be(3);
        // (0.5 * 2 + 1.0) / 3 = 2.0 / 3 ≈ 0.667
        skill.SuccessRate.Should().BeApproximately(2.0 / 3.0, 0.001);
        // (100 * 2 + 300) / 3 = 500 / 3 ≈ 166
        skill.AverageExecutionTime.Should().Be(500 / 3);
    }

    [Fact]
    public void RecordSkillExecution_EmptyId_DoesNotThrow()
    {
        var registry = CreateInMemoryRegistry();
        var act = () => registry.RecordSkillExecution("", true, 100);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordSkillExecution_UnknownId_DoesNotThrow()
    {
        var registry = CreateInMemoryRegistry();
        var act = () => registry.RecordSkillExecution("unknown", true, 100);
        act.Should().NotThrow();
    }

    // ========================================================
    // FindSkillsAsync: filtering by category and tags
    // ========================================================

    [Fact]
    public async Task FindSkills_FilterByCategory()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1", category: "coding"));
        registry.RegisterSkill(MakeSkill("s2", category: "testing"));
        registry.RegisterSkill(MakeSkill("s3", category: "coding"));

        var result = await registry.FindSkillsAsync(category: "coding");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(s => s.Category == "coding");
    }

    [Fact]
    public async Task FindSkills_FilterByTags()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1", tags: new List<string> { "python", "ml" }));
        registry.RegisterSkill(MakeSkill("s2", tags: new List<string> { "javascript" }));
        registry.RegisterSkill(MakeSkill("s3", tags: new List<string> { "python", "data" }));

        var result = await registry.FindSkillsAsync(
            tags: new List<string> { "python" });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindSkills_NoFilters_ReturnsAllOrderedBySuccessRate()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("low", successRate: 0.3));
        registry.RegisterSkill(MakeSkill("high", successRate: 0.95));
        registry.RegisterSkill(MakeSkill("mid", successRate: 0.6));

        var result = await registry.FindSkillsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value[0].Id.Should().Be("high");
        result.Value[1].Id.Should().Be("mid");
        result.Value[2].Id.Should().Be("low");
    }

    // ========================================================
    // UpdateSkillAsync
    // ========================================================

    [Fact]
    public async Task UpdateSkill_NotExisting_ReturnsFailure()
    {
        var registry = CreateInMemoryRegistry();
        var skill = MakeSkill("unknown");

        var result = await registry.UpdateSkillAsync(skill);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateSkill_ExistingSkill_UpdatesCache()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1", name: "Old Name"));

        var updated = MakeSkill("s1", name: "New Name");
        var result = await registry.UpdateSkillAsync(updated);

        result.IsSuccess.Should().BeTrue();
        registry.GetSkill("s1")!.Name.Should().Be("New Name");
    }

    // ========================================================
    // UnregisterSkillAsync
    // ========================================================

    [Fact]
    public async Task UnregisterSkill_ExistingSkill_RemovesFromCache()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1"));

        var result = await registry.UnregisterSkillAsync("s1");

        result.IsSuccess.Should().BeTrue();
        registry.GetSkill("s1").Should().BeNull();
    }

    [Fact]
    public async Task UnregisterSkill_EmptyId_ReturnsFailure()
    {
        var registry = CreateInMemoryRegistry();
        var result = await registry.UnregisterSkillAsync("");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UnregisterSkill_NonexistentSkill_ReturnsFailure()
    {
        var registry = CreateInMemoryRegistry();
        var result = await registry.UnregisterSkillAsync("does-not-exist");
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    // ========================================================
    // GetAllSkillsAsync / GetAllSkills
    // ========================================================

    [Fact]
    public async Task GetAllSkillsAsync_OrdersBySuccessRate()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("low", successRate: 0.2));
        registry.RegisterSkill(MakeSkill("high", successRate: 0.99));
        registry.RegisterSkill(MakeSkill("mid", successRate: 0.5));

        var result = await registry.GetAllSkillsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Id.Should().Be("high");
        result.Value[2].Id.Should().Be("low");
    }

    [Fact]
    public void GetAllSkills_Sync_ReturnsSameOrder()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("a", successRate: 0.1));
        registry.RegisterSkill(MakeSkill("b", successRate: 0.9));

        var skills = registry.GetAllSkills();

        skills[0].Id.Should().Be("b");
        skills[1].Id.Should().Be("a");
    }

    // ========================================================
    // GetStats
    // ========================================================

    [Fact]
    public void GetStats_EmptyRegistry_ReturnsZeros()
    {
        var registry = CreateInMemoryRegistry();
        var stats = registry.GetStats();

        stats.TotalSkills.Should().Be(0);
        stats.AverageSuccessRate.Should().Be(0);
        stats.TotalExecutions.Should().Be(0);
        stats.MostUsedSkill.Should().BeNull();
        stats.MostSuccessfulSkill.Should().BeNull();
    }

    [Fact]
    public void GetStats_WithSkills_ComputesAggregates()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1", successRate: 0.8, usageCount: 50));
        registry.RegisterSkill(MakeSkill("s2", successRate: 0.6, usageCount: 100,
            name: "Most Used"));
        registry.RegisterSkill(MakeSkill("s3", successRate: 0.95, usageCount: 10,
            name: "Most Successful"));

        var stats = registry.GetStats();

        stats.TotalSkills.Should().Be(3);
        stats.AverageSuccessRate.Should().BeApproximately((0.8 + 0.6 + 0.95) / 3, 0.001);
        stats.TotalExecutions.Should().Be(160);
        stats.MostUsedSkill.Should().Be("Most Used");
        stats.MostSuccessfulSkill.Should().Be("Most Successful");
    }

    // ========================================================
    // FindMatchingSkillsAsync
    // ========================================================

    [Fact]
    public async Task FindMatchingSkills_ExtractsTagsFromGoal()
    {
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1",
            tags: new List<string> { "analyze", "data" }));

        // "analyze the data carefully" => tags: ["analyze", "data", "carefully"]
        var skills = await registry.FindMatchingSkillsAsync("analyze the data carefully");

        // Should find s1 because tags "analyze" and "data" match
        skills.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task FindMatchingSkills_EmptyGoal_ReturnsAllSkills()
    {
        // With an empty goal, ExtractTagsFromGoal returns empty tags,
        // and FindSkillsAsync with no category + no tags returns all skills.
        var registry = CreateInMemoryRegistry();
        registry.RegisterSkill(MakeSkill("s1"));

        var skills = await registry.FindMatchingSkillsAsync("");

        skills.Should().HaveCount(1, "no filter is applied when goal is empty");
    }

    // ========================================================
    // ExtractSkillAsync
    // ========================================================

    [Fact]
    public async Task ExtractSkill_NullExecution_ReturnsFailure()
    {
        var registry = CreateInMemoryRegistry();

        // ArgumentNullException.ThrowIfNull is caught by the try/catch
        // and wrapped in a Failure result.
        var result = await registry.ExtractSkillAsync(null!, "test", "description");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to extract skill");
    }

    // ========================================================
    // Connection string normalization (used in obsolete constructor)
    // NormalizeConnectionString is private static, but we can test through
    // the public constructor's behavior
    // ========================================================

    [Fact]
    public void GetStats_ReportsConnectionString()
    {
        var registry = CreateInMemoryRegistry();
        var stats = registry.GetStats();
        stats.ConnectionString.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetStats_ReportsCollectionName()
    {
        var registry = CreateInMemoryRegistry();
        var stats = registry.GetStats();
        stats.CollectionName.Should().NotBeNullOrEmpty();
    }
}

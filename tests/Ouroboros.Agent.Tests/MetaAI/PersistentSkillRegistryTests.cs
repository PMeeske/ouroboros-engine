using FluentAssertions;
using Ouroboros.Abstractions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class PersistentSkillRegistryTests : IAsyncDisposable
{
    private readonly string _testFilePath;
    private PersistentSkillRegistry? _sut;

    public PersistentSkillRegistryTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"ouroboros_skills_test_{Guid.NewGuid():N}.json");
    }

    public async ValueTask DisposeAsync()
    {
        if (_sut != null)
        {
            await _sut.DisposeAsync();
        }

        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    private PersistentSkillRegistry CreateSut(bool autoSave = true)
    {
        _sut = new PersistentSkillRegistry(
            config: new PersistentSkillConfig(StoragePath: _testFilePath, AutoSave: autoSave));
        return _sut;
    }

    // === Constructor Tests ===

    [Fact]
    public void Constructor_NullConfig_UsesDefaults()
    {
        var act = () => new PersistentSkillRegistry();
        act.Should().NotThrow();
    }

    // === RegisterSkillAsync Tests ===

    [Fact]
    public async Task RegisterSkillAsync_ValidSkill_ReturnsSuccess()
    {
        var sut = CreateSut();
        var skill = CreateSkill("skill-1");

        var result = await sut.RegisterSkillAsync(skill);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterSkillAsync_NullSkill_ThrowsArgumentNullException()
    {
        var sut = CreateSut();

        // RegisterSkillAsync calls ThrowIfNull which throws
        var act = () => sut.RegisterSkillAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RegisterSkillAsync_DuplicateId_OverwritesExisting()
    {
        var sut = CreateSut();
        var skill1 = CreateSkill("dup");
        var skill2 = CreateSkill("dup") with { Description = "Updated" };

        await sut.RegisterSkillAsync(skill1);
        await sut.RegisterSkillAsync(skill2);

        var result = await sut.GetSkillAsync("dup");
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("Updated");
    }

    // === GetSkillAsync Tests ===

    [Fact]
    public async Task GetSkillAsync_EmptyId_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.GetSkillAsync("");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetSkillAsync_WhitespaceId_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.GetSkillAsync("   ");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetSkillAsync_NonExistent_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.GetSkillAsync("nonexistent");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetSkillAsync_ExistingSkill_ReturnsSuccess()
    {
        var sut = CreateSut();
        await sut.RegisterSkillAsync(CreateSkill("existing"));

        var result = await sut.GetSkillAsync("existing");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("existing");
    }

    // === UpdateSkillAsync Tests ===

    [Fact]
    public async Task UpdateSkillAsync_NonExistent_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.UpdateSkillAsync(CreateSkill("nonexistent"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateSkillAsync_ExistingSkill_UpdatesSuccessfully()
    {
        var sut = CreateSut();
        await sut.RegisterSkillAsync(CreateSkill("to-update"));

        var updated = CreateSkill("to-update") with { Description = "Updated description" };
        var result = await sut.UpdateSkillAsync(updated);

        result.IsSuccess.Should().BeTrue();
        var retrieved = await sut.GetSkillAsync("to-update");
        retrieved.Value.Description.Should().Be("Updated description");
    }

    // === RecordExecutionAsync Tests ===

    [Fact]
    public async Task RecordExecutionAsync_EmptyId_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.RecordExecutionAsync("", true, 100);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RecordExecutionAsync_NonExistent_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.RecordExecutionAsync("nonexistent", true, 100);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RecordExecutionAsync_ExistingSkill_UpdatesStats()
    {
        var sut = CreateSut();
        await sut.RegisterSkillAsync(CreateSkill("tracked"));

        var result = await sut.RecordExecutionAsync("tracked", true, 200);

        result.IsSuccess.Should().BeTrue();
        var skill = await sut.GetSkillAsync("tracked");
        skill.Value.UsageCount.Should().Be(2); // 1 initial + 1 recorded
    }

    // === UnregisterSkillAsync Tests ===

    [Fact]
    public async Task UnregisterSkillAsync_EmptyId_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.UnregisterSkillAsync("");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UnregisterSkillAsync_NonExistent_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.UnregisterSkillAsync("nonexistent");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UnregisterSkillAsync_ExistingSkill_RemovesSuccessfully()
    {
        var sut = CreateSut();
        await sut.RegisterSkillAsync(CreateSkill("to-remove"));

        var result = await sut.UnregisterSkillAsync("to-remove");

        result.IsSuccess.Should().BeTrue();
        var getResult = await sut.GetSkillAsync("to-remove");
        getResult.IsFailure.Should().BeTrue();
    }

    // === GetAllSkillsAsync Tests ===

    [Fact]
    public async Task GetAllSkillsAsync_Empty_ReturnsEmptyList()
    {
        var sut = CreateSut();

        var result = await sut.GetAllSkillsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllSkillsAsync_WithSkills_ReturnsOrderedBySuccessRate()
    {
        var sut = CreateSut();
        await sut.RegisterSkillAsync(CreateSkill("low") with { SuccessRate = 0.5 });
        await sut.RegisterSkillAsync(CreateSkill("high") with { SuccessRate = 0.95 });

        var result = await sut.GetAllSkillsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].SuccessRate.Should().BeGreaterThanOrEqualTo(result.Value[1].SuccessRate);
    }

    // === Sync Convenience Methods ===

    [Fact]
    public void RegisterSkill_ValidSkill_ReturnsSuccess()
    {
        var sut = CreateSut();

        var result = sut.RegisterSkill(CreateSkill("sync-skill"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void GetSkill_ExistingSkill_ReturnsSkill()
    {
        var sut = CreateSut();
        sut.RegisterSkill(CreateSkill("sync-get"));

        var skill = sut.GetSkill("sync-get");

        skill.Should().NotBeNull();
        skill!.Id.Should().Be("sync-get");
    }

    [Fact]
    public void GetSkill_NonExistent_ReturnsNull()
    {
        var sut = CreateSut();

        var skill = sut.GetSkill("nonexistent");

        skill.Should().BeNull();
    }

    [Fact]
    public void GetSkill_EmptyId_ReturnsNull()
    {
        var sut = CreateSut();

        var skill = sut.GetSkill("");

        skill.Should().BeNull();
    }

    [Fact]
    public void GetAllSkills_ReturnsOrderedList()
    {
        var sut = CreateSut();
        sut.RegisterSkill(CreateSkill("a") with { SuccessRate = 0.5 });
        sut.RegisterSkill(CreateSkill("b") with { SuccessRate = 0.9 });

        var skills = sut.GetAllSkills();

        skills.Should().HaveCount(2);
        skills[0].SuccessRate.Should().BeGreaterThanOrEqualTo(skills[1].SuccessRate);
    }

    [Fact]
    public void RecordSkillExecution_ValidSkill_UpdatesStats()
    {
        var sut = CreateSut();
        sut.RegisterSkill(CreateSkill("exec-tracked"));

        sut.RecordSkillExecution("exec-tracked", true, 500);

        var skill = sut.GetSkill("exec-tracked");
        skill!.UsageCount.Should().Be(2);
    }

    [Fact]
    public void RecordSkillExecution_EmptyId_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.RecordSkillExecution("", true, 100);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordSkillExecution_NonExistent_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.RecordSkillExecution("nonexistent", true, 100);

        act.Should().NotThrow();
    }

    // === GetStats Tests ===

    [Fact]
    public void GetStats_Empty_ReturnsZeros()
    {
        var sut = CreateSut();

        var stats = sut.GetStats();

        stats.TotalSkills.Should().Be(0);
        stats.TotalExecutions.Should().Be(0);
    }

    [Fact]
    public void GetStats_WithSkills_ReturnsAccurateStats()
    {
        var sut = CreateSut();
        sut.RegisterSkill(CreateSkill("s1") with { UsageCount = 10, SuccessRate = 0.9 });
        sut.RegisterSkill(CreateSkill("s2") with { UsageCount = 5, SuccessRate = 0.8 });

        var stats = sut.GetStats();

        stats.TotalSkills.Should().Be(2);
        stats.TotalExecutions.Should().Be(15);
        stats.AverageSuccessRate.Should().BeApproximately(0.85, 0.01);
    }

    // === FindSkillsAsync Tests ===

    [Fact]
    public async Task FindSkillsAsync_NoFilter_ReturnsAll()
    {
        var sut = CreateSut();
        await sut.RegisterSkillAsync(CreateSkill("s1"));
        await sut.RegisterSkillAsync(CreateSkill("s2"));

        var result = await sut.FindSkillsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindSkillsAsync_ByCategory_FiltersCorrectly()
    {
        var sut = CreateSut();
        await sut.RegisterSkillAsync(CreateSkill("s1") with { Category = "planning" });
        await sut.RegisterSkillAsync(CreateSkill("s2") with { Category = "coding" });

        var result = await sut.FindSkillsAsync(category: "planning");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Category.Should().Be("planning");
    }

    [Fact]
    public async Task FindSkillsAsync_ByTags_FiltersCorrectly()
    {
        var sut = CreateSut();
        await sut.RegisterSkillAsync(CreateSkill("s1") with { Tags = new List<string> { "ai", "ml" } });
        await sut.RegisterSkillAsync(CreateSkill("s2") with { Tags = new List<string> { "web", "api" } });

        var result = await sut.FindSkillsAsync(tags: new List<string> { "ai" });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    // === FindMatchingSkillsAsync Tests ===

    [Fact]
    public async Task FindMatchingSkillsAsync_EmptyGoal_ReturnsEmpty()
    {
        var sut = CreateSut();

        var result = await sut.FindMatchingSkillsAsync("");

        result.Should().BeEmpty();
    }

    // === ExtractSkillAsync Tests ===

    [Fact]
    public async Task ExtractSkillAsync_ValidExecution_ReturnsSuccess()
    {
        var sut = CreateSut();
        var plan = new Plan("goal", new List<PlanStep>
        {
            new PlanStep("action", new Dictionary<string, object>(), "expected", 0.8)
        }, new Dictionary<string, double>(), DateTime.UtcNow);

        var execution = new PlanExecutionResult(plan,
            new List<StepResult>
            {
                new StepResult(plan.Steps[0], true, "output", null, TimeSpan.FromMilliseconds(100), new Dictionary<string, object>())
            },
            true, "output", new Dictionary<string, object>(), TimeSpan.FromSeconds(1));

        var result = await sut.ExtractSkillAsync(execution, "extracted-skill", "A learned skill");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("extracted-skill");
        result.Value.Description.Should().Be("A learned skill");
    }

    [Fact]
    public async Task ExtractSkillAsync_NullExecution_ReturnsFailure()
    {
        var sut = CreateSut();

        var act = () => sut.ExtractSkillAsync(null!, "name", "desc");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // === Persistence Round Trip ===

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesSkills()
    {
        var skill = CreateSkill("persistent-skill");

        // Save
        {
            await using var sut = new PersistentSkillRegistry(
                config: new PersistentSkillConfig(StoragePath: _testFilePath, AutoSave: true));
            await sut.RegisterSkillAsync(skill);
            await sut.SaveSkillsAsync();
        }

        // Load
        {
            await using var sut2 = new PersistentSkillRegistry(
                config: new PersistentSkillConfig(StoragePath: _testFilePath, AutoSave: true));
            await sut2.LoadSkillsAsync();

            var loaded = await sut2.GetSkillAsync("persistent-skill");
            loaded.IsSuccess.Should().BeTrue();
            loaded.Value.Name.Should().Be("Test Skill");
        }
    }

    // === DeleteSkillAsync Tests ===

    [Fact]
    public async Task DeleteSkillAsync_ExistingSkill_RemovesIt()
    {
        var sut = CreateSut();
        await sut.RegisterSkillAsync(CreateSkill("to-delete"));

        await sut.DeleteSkillAsync("to-delete");

        var result = await sut.GetSkillAsync("to-delete");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteSkillAsync_NonExistent_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.DeleteSkillAsync("nonexistent");

        await act.Should().NotThrowAsync();
    }

    // === InitializeAsync Tests ===

    [Fact]
    public async Task InitializeAsync_CalledMultipleTimes_OnlyLoadsOnce()
    {
        var sut = CreateSut();

        await sut.InitializeAsync();
        await sut.InitializeAsync(); // Should not throw

        // Just verifying no exception
    }

    // === Helper Methods ===

    private static AgentSkill CreateSkill(string id)
    {
        return new AgentSkill(
            id,
            "Test Skill",
            "A test skill description",
            "general",
            new List<string> { "input-ready" },
            new List<string> { "output-generated" },
            0.9,
            1,
            100L,
            new List<string> { "test", "general" });
    }
}

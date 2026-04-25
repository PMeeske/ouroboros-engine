using Ouroboros.Agent.MetaAI;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class SkillRegistryTests
{
    #region Constructor

    [Fact]
    public void Constructor_WithoutEmbedding_ShouldInitialize()
    {
        // Act
        var registry = new SkillRegistry();

        // Assert
        registry.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithEmbedding_ShouldInitialize()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingModel>();

        // Act
        var registry = new SkillRegistry(mockEmbedding.Object);

        // Assert
        registry.Should().NotBeNull();
    }

    #endregion

    #region RegisterSkillAsync

    [Fact]
    public async Task RegisterSkillAsync_WithNullSkill_ShouldThrowArgumentNullException()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        Func<Task> act = async () => await registry.RegisterSkillAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RegisterSkillAsync_ValidSkill_ShouldSucceed()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("skill-1", "TestSkill", "Description", "general",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string>());

        // Act
        var result = await registry.RegisterSkillAsync(skill);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region GetSkillAsync

    [Fact]
    public async Task GetSkillAsync_WithEmptyId_ShouldReturnFailure()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = await registry.GetSkillAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetSkillAsync_WithWhitespaceId_ShouldReturnFailure()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = await registry.GetSkillAsync("   ");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetSkillAsync_NonExistentSkill_ShouldReturnFailure()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = await registry.GetSkillAsync("non-existent");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetSkillAsync_ExistingSkill_ShouldReturnSuccess()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("skill-1", "TestSkill", "Description", "general",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string>());
        await registry.RegisterSkillAsync(skill);

        // Act
        var result = await registry.GetSkillAsync("skill-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("TestSkill");
    }

    #endregion

    #region FindSkillsAsync

    [Fact]
    public async Task FindSkillsAsync_NoSkills_ShouldReturnEmptyList()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = await registry.FindSkillsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task FindSkillsAsync_ByCategory_ShouldFilter()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill1 = new AgentSkill("s1", "Skill1", "Desc", "code",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string>());
        var skill2 = new AgentSkill("s2", "Skill2", "Desc", "general",
            new List<string>(), new List<string>(), 0.7, 0, 0, new List<string>());
        await registry.RegisterSkillAsync(skill1);
        await registry.RegisterSkillAsync(skill2);

        // Act
        var result = await registry.FindSkillsAsync("code");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Name.Should().Be("Skill1");
    }

    [Fact]
    public async Task FindSkillsAsync_ByTags_ShouldFilter()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string> { "tag1", "tag2" });
        await registry.RegisterSkillAsync(skill);

        // Act
        var result = await registry.FindSkillsAsync(null, new List<string> { "tag1" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task FindSkillsAsync_ShouldOrderBySuccessRate()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill1 = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 0.9, 0, 0, new List<string>());
        var skill2 = new AgentSkill("s2", "Skill2", "Desc", "general",
            new List<string>(), new List<string>(), 0.5, 0, 0, new List<string>());
        await registry.RegisterSkillAsync(skill1);
        await registry.RegisterSkillAsync(skill2);

        // Act
        var result = await registry.FindSkillsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value[0].Name.Should().Be("Skill1");
        result.Value[1].Name.Should().Be("Skill2");
    }

    #endregion

    #region UpdateSkillAsync

    [Fact]
    public async Task UpdateSkillAsync_WithNullSkill_ShouldThrowArgumentNullException()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        Func<Task> act = async () => await registry.UpdateSkillAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateSkillAsync_NonExistentSkill_ShouldReturnFailure()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string>());

        // Act
        var result = await registry.UpdateSkillAsync(skill);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSkillAsync_ExistingSkill_ShouldUpdate()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string>());
        await registry.RegisterSkillAsync(skill);
        var updated = skill with { SuccessRate = 0.95 };

        // Act
        var result = await registry.UpdateSkillAsync(updated);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var getResult = await registry.GetSkillAsync("s1");
        getResult.Value.SuccessRate.Should().Be(0.95);
    }

    #endregion

    #region RecordExecutionAsync

    [Fact]
    public async Task RecordExecutionAsync_WithEmptyId_ShouldReturnFailure()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = await registry.RecordExecutionAsync("", true, 100);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RecordExecutionAsync_NonExistentSkill_ShouldReturnFailure()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = await registry.RecordExecutionAsync("non-existent", true, 100);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RecordExecutionAsync_ExistingSkill_ShouldUpdateStats()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 0.8, 5, 100, new List<string>());
        await registry.RegisterSkillAsync(skill);

        // Act
        var result = await registry.RecordExecutionAsync("s1", true, 150);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var getResult = await registry.GetSkillAsync("s1");
        getResult.Value.UsageCount.Should().Be(6);
    }

    [Fact]
    public async Task RecordExecutionAsync_FailedExecution_ShouldUpdateSuccessRate()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 1.0, 1, 100, new List<string>());
        await registry.RegisterSkillAsync(skill);

        // Act
        var result = await registry.RecordExecutionAsync("s1", false, 200);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var getResult = await registry.GetSkillAsync("s1");
        getResult.Value.SuccessRate.Should().Be(0.5);
    }

    #endregion

    #region UnregisterSkillAsync

    [Fact]
    public async Task UnregisterSkillAsync_WithEmptyId_ShouldReturnFailure()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = await registry.UnregisterSkillAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UnregisterSkillAsync_NonExistentSkill_ShouldReturnFailure()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = await registry.UnregisterSkillAsync("non-existent");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UnregisterSkillAsync_ExistingSkill_ShouldRemove()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string>());
        await registry.RegisterSkillAsync(skill);

        // Act
        var result = await registry.UnregisterSkillAsync("s1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var getResult = await registry.GetSkillAsync("s1");
        getResult.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region GetAllSkillsAsync

    [Fact]
    public async Task GetAllSkillsAsync_NoSkills_ShouldReturnEmpty()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = await registry.GetAllSkillsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllSkillsAsync_WithSkills_ShouldReturnAll()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill1 = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string>());
        var skill2 = new AgentSkill("s2", "Skill2", "Desc", "general",
            new List<string>(), new List<string>(), 0.9, 0, 0, new List<string>());
        await registry.RegisterSkillAsync(skill1);
        await registry.RegisterSkillAsync(skill2);

        // Act
        var result = await registry.GetAllSkillsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    #endregion

    #region Sync Methods

    [Fact]
    public void RegisterSkill_Sync_WithNullSkill_ShouldThrowArgumentNullException()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        Action act = () => registry.RegisterSkill(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterSkill_Sync_ValidSkill_ShouldSucceed()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string>());

        // Act
        var result = registry.RegisterSkill(skill);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void GetSkill_Sync_WithEmptyId_ShouldReturnNull()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = registry.GetSkill("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSkill_Sync_NonExistent_ShouldReturnNull()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = registry.GetSkill("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSkill_Sync_Existing_ShouldReturnSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string>());
        registry.RegisterSkill(skill);

        // Act
        var result = registry.GetSkill("s1");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Skill1");
    }

    [Fact]
    public void GetAllSkills_Sync_NoSkills_ShouldReturnEmpty()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = registry.GetAllSkills();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllSkills_Sync_WithSkills_ShouldReturnAll()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string>());
        registry.RegisterSkill(skill);

        // Act
        var result = registry.GetAllSkills();

        // Assert
        result.Should().ContainSingle();
    }

    [Fact]
    public void RecordSkillExecution_Sync_WithEmptyId_ShouldDoNothing()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        registry.RecordSkillExecution("", true, 100);

        // Assert - no exception
    }

    [Fact]
    public void RecordSkillExecution_Sync_ExistingSkill_ShouldUpdateStats()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("s1", "Skill1", "Desc", "general",
            new List<string>(), new List<string>(), 0.8, 5, 100, new List<string>());
        registry.RegisterSkill(skill);

        // Act
        registry.RecordSkillExecution("s1", true, 150);

        // Assert
        var updated = registry.GetSkill("s1");
        updated!.UsageCount.Should().Be(6);
    }

    #endregion

    #region FindMatchingSkillsAsync

    [Fact]
    public async Task FindMatchingSkillsAsync_WithGoal_ShouldReturnSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new AgentSkill("s1", "Skill1", "Description of a test skill", "general",
            new List<string>(), new List<string>(), 0.8, 0, 0, new List<string>());
        await registry.RegisterSkillAsync(skill);

        // Act
        var result = await registry.FindMatchingSkillsAsync("test skill");

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FindMatchingSkillsAsync_NoMatches_ShouldReturnEmpty()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = await registry.FindMatchingSkillsAsync("nonexistent query");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ExtractSkillAsync

    [Fact]
    public async Task ExtractSkillAsync_WithNullExecution_ShouldThrowArgumentNullException()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        Func<Task> act = async () => await registry.ExtractSkillAsync(null!, "name", "desc");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion
}

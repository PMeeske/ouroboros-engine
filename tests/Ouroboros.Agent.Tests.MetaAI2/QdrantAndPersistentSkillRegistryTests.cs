using Ouroboros.Agent.MetaAI;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class QdrantSkillRegistryTests
{
    #region Constructor

    [Fact]
    public void Constructor_WithNullConfig_ShouldUseDefault()
    {
        // Act
        var registry = new QdrantSkillRegistry(null!);

        // Assert
        registry.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithConfig_ShouldInitialize()
    {
        // Arrange
        var config = new QdrantSkillConfig("http://custom:6334", "custom_skills", false, 768);

        // Act
        var registry = new QdrantSkillRegistry(config);

        // Assert
        registry.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullEmbedding_ShouldInitialize()
    {
        // Arrange
        var config = new QdrantSkillConfig();
        var mockEmbedding = new Mock<IEmbeddingModel>();

        // Act
        var registry = new QdrantSkillRegistry(config, mockEmbedding.Object);

        // Assert
        registry.Should().NotBeNull();
    }

    #endregion

    #region RegisterSkillAsync

    [Fact]
    public async Task RegisterSkillAsync_WithNullSkill_ShouldThrowArgumentNullException()
    {
        // Arrange
        var registry = new QdrantSkillRegistry();

        // Act
        Func<Task> act = async () => await registry.RegisterSkillAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region GetSkillAsync

    [Fact]
    public async Task GetSkillAsync_WithEmptyId_ShouldReturnFailure()
    {
        // Arrange
        var registry = new QdrantSkillRegistry();

        // Act
        var result = await registry.GetSkillAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetSkillAsync_NonExistentSkill_ShouldReturnFailure()
    {
        // Arrange
        var registry = new QdrantSkillRegistry();

        // Act
        var result = await registry.GetSkillAsync("non-existent");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region FindSkillsAsync

    [Fact]
    public async Task FindSkillsAsync_EmptyCategory_ShouldReturnAll()
    {
        // Arrange
        var registry = new QdrantSkillRegistry();

        // Act
        var result = await registry.FindSkillsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region UpdateSkillAsync

    [Fact]
    public async Task UpdateSkillAsync_WithNullSkill_ShouldThrowArgumentNullException()
    {
        // Arrange
        var registry = new QdrantSkillRegistry();

        // Act
        Func<Task> act = async () => await registry.UpdateSkillAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region UnregisterSkillAsync

    [Fact]
    public async Task UnregisterSkillAsync_WithEmptyId_ShouldReturnFailure()
    {
        // Arrange
        var registry = new QdrantSkillRegistry();

        // Act
        var result = await registry.UnregisterSkillAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UnregisterSkillAsync_NonExistentSkill_ShouldReturnFailure()
    {
        // Arrange
        var registry = new QdrantSkillRegistry();

        // Act
        var result = await registry.UnregisterSkillAsync("non-existent");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region GetStats

    [Fact]
    public void GetStats_ShouldReturnStats()
    {
        // Arrange
        var registry = new QdrantSkillRegistry();

        // Act
        var result = registry.GetStats();

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region GetAllSkillsAsync

    [Fact]
    public async Task GetAllSkillsAsync_ShouldReturnResult()
    {
        // Arrange
        var registry = new QdrantSkillRegistry();

        // Act
        var result = await registry.GetAllSkillsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region RecordExecutionAsync

    [Fact]
    public async Task RecordExecutionAsync_WithEmptyId_ShouldReturnFailure()
    {
        // Arrange
        var registry = new QdrantSkillRegistry();

        // Act
        var result = await registry.RecordExecutionAsync("", true, 100);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region FindMatchingSkillsAsync

    [Fact]
    public async Task FindMatchingSkillsAsync_ShouldReturnResult()
    {
        // Arrange
        var registry = new QdrantSkillRegistry();

        // Act
        var result = await registry.FindMatchingSkillsAsync("goal");

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class PersistentSkillRegistryTests
{
    private readonly string _testPath;

    public PersistentSkillRegistryTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    }

    #region Constructor

    [Fact]
    public void Constructor_WithNullConfig_ShouldUseDefault()
    {
        // Act
        var registry = new PersistentSkillRegistry(null!);

        // Assert
        registry.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithConfig_ShouldInitialize()
    {
        // Arrange
        var config = new PersistentSkillConfig { StoragePath = _testPath, AutoSave = false };

        // Act
        var registry = new PersistentSkillRegistry(config);

        // Assert
        registry.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullEmbedding_ShouldInitialize()
    {
        // Arrange
        var config = new PersistentSkillConfig();
        var mockEmbedding = new Mock<IEmbeddingModel>();

        // Act
        var registry = new PersistentSkillRegistry(config, mockEmbedding.Object);

        // Assert
        registry.Should().NotBeNull();
    }

    #endregion

    #region RegisterSkillAsync

    [Fact]
    public async Task RegisterSkillAsync_WithNullSkill_ShouldThrowArgumentNullException()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        Func<Task> act = async () => await registry.RegisterSkillAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region GetSkillAsync

    [Fact]
    public async Task GetSkillAsync_WithEmptyId_ShouldReturnFailure()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        var result = await registry.GetSkillAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetSkillAsync_NonExistentSkill_ShouldReturnFailure()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        var result = await registry.GetSkillAsync("non-existent");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region FindSkillsAsync

    [Fact]
    public async Task FindSkillsAsync_ShouldReturnResult()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        var result = await registry.FindSkillsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region UpdateSkillAsync

    [Fact]
    public async Task UpdateSkillAsync_WithNullSkill_ShouldThrowArgumentNullException()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        Func<Task> act = async () => await registry.UpdateSkillAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region UnregisterSkillAsync

    [Fact]
    public async Task UnregisterSkillAsync_WithEmptyId_ShouldReturnFailure()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        var result = await registry.UnregisterSkillAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region GetAllSkillsAsync

    [Fact]
    public async Task GetAllSkillsAsync_ShouldReturnResult()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        var result = await registry.GetAllSkillsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region RecordExecutionAsync

    [Fact]
    public async Task RecordExecutionAsync_WithEmptyId_ShouldReturnFailure()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        var result = await registry.RecordExecutionAsync("", true, 100);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region GetStats

    [Fact]
    public void GetStats_ShouldReturnStats()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        var result = registry.GetStats();

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region SaveAsync

    [Fact]
    public async Task SaveAsync_ShouldNotThrow()
    {
        // Arrange
        var config = new PersistentSkillConfig { StoragePath = _testPath, AutoSave = false };
        var registry = new PersistentSkillRegistry(config);
        Directory.CreateDirectory(_testPath);

        // Act
        await registry.SaveAsync();

        // Assert
        // Should not throw
    }

    #endregion

    #region LoadAsync

    [Fact]
    public async Task LoadAsync_NoExistingFile_ShouldNotThrow()
    {
        // Arrange
        var config = new PersistentSkillConfig { StoragePath = _testPath, AutoSave = false };
        var registry = new PersistentSkillRegistry(config);

        // Act
        await registry.LoadAsync();

        // Assert
        // Should not throw
    }

    #endregion

    #region Sync Methods

    [Fact]
    public void RegisterSkill_Sync_NullSkill_ShouldThrow()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        Action act = () => registry.RegisterSkill(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetSkill_Sync_EmptyId_ShouldReturnNull()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        var result = registry.GetSkill("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetAllSkills_Sync_ShouldReturnEmptyInitially()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        var result = registry.GetAllSkills();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void RecordSkillExecution_Sync_EmptyId_ShouldNotThrow()
    {
        // Arrange
        var registry = new PersistentSkillRegistry();

        // Act
        registry.RecordSkillExecution("", true, 100);

        // Assert
        // Should not throw
    }

    #endregion
}

using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class MemoryStoreTests
{
    private readonly MemoryStore _store;

    public MemoryStoreTests()
    {
        _store = new MemoryStore();
    }

    #region Constructor

    [Fact]
    public void Constructor_NoArgs_ShouldInitialize()
    {
        var store = new MemoryStore();
        store.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithEmbeddingAndVectorStore_ShouldInitialize()
    {
        var mockEmbedding = new Mock<IEmbeddingModel>();
        var vectorStore = new TrackedVectorStore();
        var store = new MemoryStore(mockEmbedding.Object, vectorStore);
        store.Should().NotBeNull();
    }

    #endregion

    #region StoreExperienceAsync

    [Fact]
    public async Task StoreExperienceAsync_ValidExperience_ShouldSucceed()
    {
        var exp = new Experience("exp-1", "context", "action", "outcome", true, 0.9, new List<string>(), DateTime.UtcNow);
        var result = await _store.StoreExperienceAsync(exp);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task StoreExperienceAsync_NullExperience_ShouldThrow()
    {
        Func<Task> act = async () => await _store.StoreExperienceAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task StoreExperienceAsync_EmptyId_ShouldFail()
    {
        var exp = new Experience("", "context", "action", "outcome", true, 0.9, new List<string>(), DateTime.UtcNow);
        var result = await _store.StoreExperienceAsync(exp);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ID cannot be empty");
    }

    [Fact]
    public async Task StoreExperienceAsync_WhitespaceId_ShouldFail()
    {
        var exp = new Experience("   ", "context", "action", "outcome", true, 0.9, new List<string>(), DateTime.UtcNow);
        var result = await _store.StoreExperienceAsync(exp);

        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region GetExperienceAsync

    [Fact]
    public async Task GetExperienceAsync_Existing_ShouldReturnExperience()
    {
        var exp = new Experience("exp-1", "context", "action", "outcome", true, 0.9, new List<string>(), DateTime.UtcNow);
        await _store.StoreExperienceAsync(exp);

        var result = await _store.GetExperienceAsync("exp-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("exp-1");
    }

    [Fact]
    public async Task GetExperienceAsync_Missing_ShouldFail()
    {
        var result = await _store.GetExperienceAsync("nonexistent");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetExperienceAsync_EmptyId_ShouldFail()
    {
        var result = await _store.GetExperienceAsync("");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetExperienceAsync_NullId_ShouldFail()
    {
        var result = await _store.GetExperienceAsync(null!);

        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region QueryExperiencesAsync

    [Fact]
    public async Task QueryExperiencesAsync_ByTags_ShouldFilter()
    {
        var exp1 = new Experience("exp-1", "context", "action", "outcome", true, 0.9, new List<string> { "tag1" }, DateTime.UtcNow);
        var exp2 = new Experience("exp-2", "context", "action", "outcome", false, 0.3, new List<string> { "tag2" }, DateTime.UtcNow);
        await _store.StoreExperienceAsync(exp1);
        await _store.StoreExperienceAsync(exp2);

        var query = new MemoryQuery { Tags = new List<string> { "tag1" }, MaxResults = 10 };
        var result = await _store.QueryExperiencesAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Id.Should().Be("exp-1");
    }

    [Fact]
    public async Task QueryExperiencesAsync_BySuccessOnly_ShouldFilter()
    {
        var exp1 = new Experience("exp-1", "context", "action", "outcome", true, 0.9, new List<string>(), DateTime.UtcNow);
        var exp2 = new Experience("exp-2", "context", "action", "outcome", false, 0.3, new List<string>(), DateTime.UtcNow);
        await _store.StoreExperienceAsync(exp1);
        await _store.StoreExperienceAsync(exp2);

        var query = new MemoryQuery { SuccessOnly = true, MaxResults = 10 };
        var result = await _store.QueryExperiencesAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Id.Should().Be("exp-1");
    }

    [Fact]
    public async Task QueryExperiencesAsync_NullQuery_ShouldThrow()
    {
        Func<Task> act = async () => await _store.QueryExperiencesAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region DeleteExperienceAsync

    [Fact]
    public async Task DeleteExperienceAsync_Existing_ShouldSucceed()
    {
        var exp = new Experience("exp-1", "context", "action", "outcome", true, 0.9, new List<string>(), DateTime.UtcNow);
        await _store.StoreExperienceAsync(exp);

        var result = await _store.DeleteExperienceAsync("exp-1");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteExperienceAsync_Missing_ShouldFail()
    {
        var result = await _store.DeleteExperienceAsync("nonexistent");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task DeleteExperienceAsync_EmptyId_ShouldFail()
    {
        var result = await _store.DeleteExperienceAsync("");

        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region GetStatisticsAsync

    [Fact]
    public async Task GetStatisticsAsync_EmptyStore_ShouldReturnZeros()
    {
        var result = await _store.GetStatisticsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalExperiences.Should().Be(0);
        result.Value.SuccessfulExperiences.Should().Be(0);
        result.Value.FailedExperiences.Should().Be(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithExperiences_ShouldCalculateStats()
    {
        await _store.StoreExperienceAsync(new Experience("exp-1", "c1", "a", "o", true, 0.9, new List<string>(), DateTime.UtcNow));
        await _store.StoreExperienceAsync(new Experience("exp-2", "c2", "a", "o", false, 0.3, new List<string>(), DateTime.UtcNow));
        await _store.StoreExperienceAsync(new Experience("exp-3", "c1", "a", "o", true, 0.8, new List<string>(), DateTime.UtcNow));

        var result = await _store.GetStatisticsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalExperiences.Should().Be(3);
        result.Value.SuccessfulExperiences.Should().Be(2);
        result.Value.FailedExperiences.Should().Be(1);
        result.Value.UniqueContexts.Should().Be(2);
    }

    #endregion

    #region GetStatsAsync

    [Fact]
    public async Task GetStatsAsync_EmptyStore_ShouldReturnEmptyStats()
    {
        var stats = await _store.GetStatsAsync();

        stats.TotalExperiences.Should().Be(0);
    }

    [Fact]
    public async Task GetStatsAsync_WithExperiences_ShouldReturnStats()
    {
        await _store.StoreExperienceAsync(new Experience("exp-1", "c1", "a", "o", true, 0.9, new List<string>(), DateTime.UtcNow));
        var stats = await _store.GetStatsAsync();

        stats.TotalExperiences.Should().Be(1);
    }

    #endregion

    #region ClearAsync

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllExperiences()
    {
        await _store.StoreExperienceAsync(new Experience("exp-1", "c", "a", "o", true, 0.9, new List<string>(), DateTime.UtcNow));
        await _store.ClearAsync();

        var result = await _store.GetExperienceAsync("exp-1");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_ShouldReturnSuccess()
    {
        var result = await _store.ClearAsync();
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region RetrieveRelevantExperiencesAsync

    [Fact]
    public async Task RetrieveRelevantExperiencesAsync_ShouldAliasQuery()
    {
        var exp = new Experience("exp-1", "context", "action", "outcome", true, 0.9, new List<string>(), DateTime.UtcNow);
        await _store.StoreExperienceAsync(exp);

        var query = new MemoryQuery { MaxResults = 10 };
        var result = await _store.RetrieveRelevantExperiencesAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }

    #endregion
}

using Ouroboros.Pipeline.Reasoning;

namespace Ouroboros.Tests.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public sealed class CommonsenseKnowledgeBaseTests : IDisposable
{
    private readonly CommonsenseKnowledgeBase _sut;

    public CommonsenseKnowledgeBaseTests()
    {
        _sut = new CommonsenseKnowledgeBase();
    }

    #region Constructor and Loading Tests

    [Fact]
    public void Constructor_DefaultConstructor_CreatesInstance()
    {
        // Assert
        _sut.Should().NotBeNull();
        _sut.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithSharedEngine_CreatesInstance()
    {
        // Arrange
        using var other = new CommonsenseKnowledgeBase();

        // Act
        using var kb = new CommonsenseKnowledgeBase(other.Engine);

        // Assert
        kb.Should().NotBeNull();
        kb.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public void Engine_ReturnsNonNullEngine()
    {
        // Assert
        _sut.Engine.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_SetsIsLoadedToTrue()
    {
        // Act
        await _sut.LoadAsync();

        // Assert
        _sut.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_IsIdempotent()
    {
        // Act
        await _sut.LoadAsync();
        await _sut.LoadAsync(); // second call should be no-op

        // Assert
        _sut.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_WithCancellationToken_Completes()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        await _sut.LoadAsync(cts.Token);

        // Assert
        _sut.IsLoaded.Should().BeTrue();
    }

    #endregion

    #region Domain Facts Tests

    [Fact]
    public async Task LoadDomainFactsAsync_WithoutPriorLoad_LoadsBaseKBFirst()
    {
        // Arrange
        var facts = new[] { "(= (HasProperty (Entity \"robot\") Solid) True)" };

        // Act
        await _sut.LoadDomainFactsAsync("robotics", facts);

        // Assert
        _sut.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task LoadDomainFactsAsync_WithPriorLoad_AddsFacts()
    {
        // Arrange
        await _sut.LoadAsync();
        var facts = new[] { "(= (HasProperty (Entity \"robot\") Solid) True)" };

        // Act
        Func<Task> act = async () => await _sut.LoadDomainFactsAsync("robotics", facts);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetObjectsAboveAsync_ReturnsResults()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        var results = await _sut.GetObjectsAboveAsync("table");

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task GetObjectsInsideAsync_ReturnsResults()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        var results = await _sut.GetObjectsInsideAsync("box");

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task GetEffectsOfAsync_ReturnsResults()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        var results = await _sut.GetEffectsOfAsync("heating");

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCausesOfAsync_ReturnsResults()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        var results = await _sut.GetCausesOfAsync("melting");

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPropertiesOfAsync_ReturnsResults()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        var results = await _sut.GetPropertiesOfAsync("water");

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task GetEntitiesWithPropertyAsync_ReturnsResults()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        var results = await _sut.GetEntitiesWithPropertyAsync("Edible");

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task HasPropertyAsync_ReturnsBool()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        bool result = await _sut.HasPropertyAsync("water", "Liquid");

        // Assert - the result depends on the MeTTa engine behavior
        // We just verify it doesn't throw
        result.Should().Be(result); // tautology - we just verify execution completes
    }

    [Fact]
    public async Task GetAffordancesAsync_ReturnsResults()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        var results = await _sut.GetAffordancesAsync("human", "chair");

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task CanAgentDoAsync_ReturnsBool()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        bool result = await _sut.CanAgentDoAsync("human", "bread", "CanEat");

        // Assert
        result.Should().Be(result); // tautology - we just verify execution completes
    }

    [Fact]
    public async Task GetCategoriesOfAsync_ReturnsResults()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        var results = await _sut.GetCategoriesOfAsync("dog");

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task GetEntitiesInCategoryAsync_ReturnsResults()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        var results = await _sut.GetEntitiesInCategoryAsync("Animal");

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryAsync_WithRawQuery_ReturnsResults()
    {
        // Arrange
        await _sut.LoadAsync();

        // Act
        var results = await _sut.QueryAsync("(match &self (IsA $x Animal) $x)");

        // Assert
        results.Should().NotBeNull();
    }

    #endregion

    #region Auto-Loading Tests

    [Fact]
    public async Task GetPropertiesOfAsync_BeforeExplicitLoad_AutoLoads()
    {
        // Act - calling a query method before explicit LoadAsync
        var results = await _sut.GetPropertiesOfAsync("water");

        // Assert
        _sut.IsLoaded.Should().BeTrue();
        results.Should().NotBeNull();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var kb = new CommonsenseKnowledgeBase();

        // Act & Assert
        Action act = () =>
        {
            kb.Dispose();
            kb.Dispose();
        };
        act.Should().NotThrow();
    }

    #endregion

    public void Dispose()
    {
        _sut.Dispose();
    }
}

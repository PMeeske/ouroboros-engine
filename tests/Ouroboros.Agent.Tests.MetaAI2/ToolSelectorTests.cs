using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class ToolSelectorTests
{
    private readonly ToolSelector _toolSelector;

    public ToolSelectorTests()
    {
        _toolSelector = new ToolSelector();
    }

    #region Constructor

    [Fact]
    public void Constructor_Default_ShouldInitialize()
    {
        // Act
        var selector = new ToolSelector();

        // Assert
        selector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithTools_ShouldInitialize()
    {
        // Arrange
        var tools = new List<ITool>();

        // Act
        var selector = new ToolSelector(tools);

        // Assert
        selector.Should().NotBeNull();
    }

    #endregion

    #region RegisterTool

    [Fact]
    public void RegisterTool_ShouldAddTool()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("test-tool");
        mockTool.Setup(t => t.Category).Returns(ToolCategory.General);
        mockTool.Setup(t => t.Description).Returns("A test tool");
        mockTool.Setup(t => t.IsAvailable).Returns(true);
        mockTool.Setup(t => t.AverageLatencyMs).Returns(100.0);
        mockTool.Setup(t => t.SuccessRate).Returns(0.9);
        var selector = new ToolSelector();

        // Act
        selector.RegisterTool(mockTool.Object);

        // Assert - select should include it
        var result = selector.SelectBestTool("use test-tool");
        result.Should().NotBeNull();
        result!.Name.Should().Be("test-tool");
    }

    #endregion

    #region SelectBestTool

    [Fact]
    public void SelectBestTool_WithEmptyGoal_ShouldReturnNull()
    {
        // Act
        var result = _toolSelector.SelectBestTool("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SelectBestTool_WithWhitespaceGoal_ShouldReturnNull()
    {
        // Act
        var result = _toolSelector.SelectBestTool("   ");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SelectBestTool_NoTools_ShouldReturnNull()
    {
        // Act
        var result = _toolSelector.SelectBestTool("do something");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SelectBestTool_NoMatchingGoal_ShouldReturnNull()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("tool-1");
        mockTool.Setup(t => t.Description).Returns("A tool");
        mockTool.Setup(t => t.IsAvailable).Returns(true);
        mockTool.Setup(t => t.AverageLatencyMs).Returns(100.0);
        mockTool.Setup(t => t.SuccessRate).Returns(0.9);
        _toolSelector.RegisterTool(mockTool.Object);

        // Act
        var result = _toolSelector.SelectBestTool("something completely different");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SelectBestTool_ShouldReturnToolWithHighestScore()
    {
        // Arrange
        var mockTool1 = new Mock<ITool>();
        mockTool1.Setup(t => t.Name).Returns("tool-1");
        mockTool1.Setup(t => t.Description).Returns("Fast tool for reading");
        mockTool1.Setup(t => t.IsAvailable).Returns(true);
        mockTool1.Setup(t => t.AverageLatencyMs).Returns(50.0);
        mockTool1.Setup(t => t.SuccessRate).Returns(0.95);

        var mockTool2 = new Mock<ITool>();
        mockTool2.Setup(t => t.Name).Returns("tool-2");
        mockTool2.Setup(t => t.Description).Returns("Slow tool for reading");
        mockTool2.Setup(t => t.IsAvailable).Returns(true);
        mockTool2.Setup(t => t.AverageLatencyMs).Returns(500.0);
        mockTool2.Setup(t => t.SuccessRate).Returns(0.5);

        _toolSelector.RegisterTool(mockTool1.Object);
        _toolSelector.RegisterTool(mockTool2.Object);

        // Act
        var result = _toolSelector.SelectBestTool("read data");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("tool-1");
    }

    #endregion

    #region SelectTools

    [Fact]
    public void SelectTools_WithEmptyGoal_ShouldReturnEmpty()
    {
        // Act
        var result = _toolSelector.SelectTools("", new ToolSelectionContext());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SelectTools_NoTools_ShouldReturnEmpty()
    {
        // Act
        var result = _toolSelector.SelectTools("goal", new ToolSelectionContext());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SelectTools_ShouldReturnMultipleTools()
    {
        // Arrange
        var mockTool1 = new Mock<ITool>();
        mockTool1.Setup(t => t.Name).Returns("tool-1");
        mockTool1.Setup(t => t.Description).Returns("A tool");
        mockTool1.Setup(t => t.IsAvailable).Returns(true);
        mockTool1.Setup(t => t.AverageLatencyMs).Returns(100.0);
        mockTool1.Setup(t => t.SuccessRate).Returns(0.9);

        var mockTool2 = new Mock<ITool>();
        mockTool2.Setup(t => t.Name).Returns("tool-2");
        mockTool2.Setup(t => t.Description).Returns("A tool");
        mockTool2.Setup(t => t.IsAvailable).Returns(true);
        mockTool2.Setup(t => t.AverageLatencyMs).Returns(200.0);
        mockTool2.Setup(t => t.SuccessRate).Returns(0.8);

        _toolSelector.RegisterTool(mockTool1.Object);
        _toolSelector.RegisterTool(mockTool2.Object);

        // Act
        var result = _toolSelector.SelectTools("use tool", new ToolSelectionContext());

        // Assert
        result.Should().NotBeEmpty();
    }

    #endregion

    #region GetRecommendationsAsync

    [Fact]
    public async Task GetRecommendationsAsync_WithEmptyGoal_ShouldReturnEmpty()
    {
        // Act
        var result = await _toolSelector.GetRecommendationsAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecommendationsAsync_NoTools_ShouldReturnEmpty()
    {
        // Act
        var result = await _toolSelector.GetRecommendationsAsync("goal");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecommendationsAsync_ShouldReturnOrderedRecommendations()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("tool-1");
        mockTool.Setup(t => t.Description).Returns("A tool for analysis");
        mockTool.Setup(t => t.Category).Returns(ToolCategory.Analysis);
        mockTool.Setup(t => t.IsAvailable).Returns(true);
        mockTool.Setup(t => t.AverageLatencyMs).Returns(100.0);
        mockTool.Setup(t => t.SuccessRate).Returns(0.9);
        _toolSelector.RegisterTool(mockTool.Object);

        // Act
        var result = await _toolSelector.GetRecommendationsAsync("analyze data");

        // Assert
        result.Should().NotBeEmpty();
        result[0].ToolName.Should().Be("tool-1");
        result[0].IsRecommended.Should().BeTrue();
    }

    #endregion
}

using FluentAssertions;
using NSubstitute;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Pipeline.WorldModel;
using Ouroboros.Tools;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class ToolSelectionStateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var goal = Goal.Atomic("Test goal");
        var selection = ToolSelection.Empty;
        var timestamp = DateTime.UtcNow;

        // Act
        var state = new ToolSelectionState(goal, selection, timestamp);

        // Assert
        state.Goal.Should().Be(goal);
        state.Selection.Should().Be(selection);
        state.Timestamp.Should().Be(timestamp);
        state.Kind.Should().Be("ToolSelection");
    }

    [Fact]
    public void Text_ContainsToolCountAndGoalDescription()
    {
        // Arrange
        var goal = Goal.Atomic("My goal");
        var selection = ToolSelection.Empty;

        // Act
        var state = new ToolSelectionState(goal, selection, DateTime.UtcNow);

        // Assert
        state.Text.Should().Contain("0 tools");
        state.Text.Should().Contain("My goal");
    }

    [Fact]
    public void GetSummary_ReturnsMeaningfulSummary()
    {
        // Arrange
        var goal = Goal.Atomic("Search the web");
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("search_tool");
        var selection = new ToolSelection(
            new[] { tool },
            "Selected search_tool",
            0.85,
            [],
            []);

        // Act
        var state = new ToolSelectionState(goal, selection, DateTime.UtcNow);
        var summary = state.GetSummary();

        // Assert
        summary.Should().Contain("1 tools");
        summary.Should().Contain("Search the web");
        summary.Should().Contain("85");
    }
}

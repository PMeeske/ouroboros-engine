namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.Planning;
using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class ToolSelectionStateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var goal = CreateTestGoal("Analyze data");
        var selection = ToolSelection.Empty;
        var timestamp = DateTime.UtcNow;

        // Act
        var state = new ToolSelectionState(goal, selection, timestamp);

        // Assert
        state.Goal.Should().Be(goal);
        state.Selection.Should().Be(selection);
        state.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void Kind_IsToolSelection()
    {
        // Arrange
        var state = CreateTestState("Test goal");

        // Assert
        state.Kind.Should().Be("ToolSelection");
    }

    [Fact]
    public void Text_ContainsToolCountAndGoalDescription()
    {
        // Arrange
        var state = CreateTestState("Analyze data");

        // Assert
        state.Text.Should().Contain("Analyze data");
        state.Text.Should().Contain("tools");
    }

    [Fact]
    public void GetSummary_ContainsGoalDescription()
    {
        // Arrange
        var state = CreateTestState("Process documents");

        // Act
        var summary = state.GetSummary();

        // Assert
        summary.Should().Contain("Process documents");
        summary.Should().Contain("tools");
        summary.Should().Contain("confidence");
    }

    [Fact]
    public void GetSummary_MatchesTextProperty()
    {
        // Arrange
        var state = CreateTestState("Test goal");

        // Act
        var summary = state.GetSummary();

        // Assert
        summary.Should().Be(state.Text);
    }

    [Fact]
    public void Equality_IdenticalValues_AreEqual()
    {
        // Arrange
        var goal = CreateTestGoal("goal");
        var selection = ToolSelection.Empty;
        var ts = DateTime.UtcNow;
        var a = new ToolSelectionState(goal, selection, ts);
        var b = new ToolSelectionState(goal, selection, ts);

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = CreateTestState("original");
        var newTimestamp = DateTime.UtcNow.AddHours(1);

        // Act
        var modified = original with { Timestamp = newTimestamp };

        // Assert
        modified.Timestamp.Should().Be(newTimestamp);
        modified.Goal.Should().Be(original.Goal);
    }

    private static ToolSelectionState CreateTestState(string goalDescription)
    {
        var goal = CreateTestGoal(goalDescription);
        return new ToolSelectionState(goal, ToolSelection.Empty, DateTime.UtcNow);
    }

    private static Goal CreateTestGoal(string description)
    {
        return new Goal(
            Guid.NewGuid(),
            description,
            Array.Empty<Goal>(),
            _ => true);
    }
}

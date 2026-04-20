using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Tests.Metacognition;

[Trait("Category", "Unit")]
public sealed class InternalStateTests
{
    [Fact]
    public void Initial_ReturnsDefaultState()
    {
        // Act
        var state = InternalState.Initial();

        // Assert
        state.Id.Should().NotBeEmpty();
        state.ActiveGoals.Should().BeEmpty();
        state.CurrentFocus.Should().Be("None");
        state.CognitiveLoad.Should().Be(0.0);
        state.EmotionalValence.Should().Be(0.0);
        state.AttentionDistribution.Should().BeEmpty();
        state.WorkingMemoryItems.Should().BeEmpty();
        state.Mode.Should().Be(ProcessingMode.Reactive);
    }

    [Fact]
    public void Snapshot_CreatesNewIdAndTimestamp()
    {
        // Arrange
        var original = InternalState.Initial();

        // Act
        var snapshot = original.Snapshot();

        // Assert
        snapshot.Id.Should().NotBe(original.Id);
        snapshot.Timestamp.Should().BeOnOrAfter(original.Timestamp);
        snapshot.CurrentFocus.Should().Be(original.CurrentFocus);
        snapshot.CognitiveLoad.Should().Be(original.CognitiveLoad);
    }

    [Fact]
    public void WithGoal_AddsGoalToList()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var updated = state.WithGoal("Solve problem");

        // Assert
        updated.ActiveGoals.Should().Contain("Solve problem");
        state.ActiveGoals.Should().BeEmpty(); // immutable
    }

    [Fact]
    public void WithGoal_WithNullOrWhitespace_ReturnsSameInstance()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var withNull = state.WithGoal(null!);
        var withEmpty = state.WithGoal("");
        var withWhitespace = state.WithGoal("   ");

        // Assert
        withNull.Should().BeSameAs(state);
        withEmpty.Should().BeSameAs(state);
        withWhitespace.Should().BeSameAs(state);
    }

    [Fact]
    public void WithoutGoal_RemovesGoalFromList()
    {
        // Arrange
        var state = InternalState.Initial().WithGoal("Goal A").WithGoal("Goal B");

        // Act
        var updated = state.WithoutGoal("Goal A");

        // Assert
        updated.ActiveGoals.Should().NotContain("Goal A");
        updated.ActiveGoals.Should().Contain("Goal B");
    }

    [Fact]
    public void WithFocus_UpdatesFocus()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var updated = state.WithFocus("Analysis");

        // Assert
        updated.CurrentFocus.Should().Be("Analysis");
    }

    [Fact]
    public void WithFocus_WithNull_SetsNone()
    {
        // Act
        var state = InternalState.Initial().WithFocus(null!);

        // Assert
        state.CurrentFocus.Should().Be("None");
    }

    [Fact]
    public void WithCognitiveLoad_ClampsToValidRange()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var tooHigh = state.WithCognitiveLoad(1.5);
        var tooLow = state.WithCognitiveLoad(-0.5);
        var normal = state.WithCognitiveLoad(0.7);

        // Assert
        tooHigh.CognitiveLoad.Should().Be(1.0);
        tooLow.CognitiveLoad.Should().Be(0.0);
        normal.CognitiveLoad.Should().Be(0.7);
    }

    [Fact]
    public void WithValence_ClampsToValidRange()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var tooHigh = state.WithValence(2.0);
        var tooLow = state.WithValence(-2.0);
        var normal = state.WithValence(-0.5);

        // Assert
        tooHigh.EmotionalValence.Should().Be(1.0);
        tooLow.EmotionalValence.Should().Be(-1.0);
        normal.EmotionalValence.Should().Be(-0.5);
    }

    [Fact]
    public void WithWorkingMemoryItem_AddsItemToMemory()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var updated = state
            .WithWorkingMemoryItem("item1")
            .WithWorkingMemoryItem("item2");

        // Assert
        updated.WorkingMemoryItems.Should().HaveCount(2);
        updated.WorkingMemoryItems.Should().Contain("item1");
        updated.WorkingMemoryItems.Should().Contain("item2");
    }

    [Fact]
    public void WithWorkingMemoryItem_WithNullOrWhitespace_ReturnsSameInstance()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act & Assert
        state.WithWorkingMemoryItem(null!).Should().BeSameAs(state);
        state.WithWorkingMemoryItem("").Should().BeSameAs(state);
        state.WithWorkingMemoryItem("  ").Should().BeSameAs(state);
    }

    [Fact]
    public void WithAttention_SetsDistribution()
    {
        // Arrange
        var distribution = ImmutableDictionary<string, double>.Empty
            .Add("task1", 0.7)
            .Add("task2", 0.3);

        // Act
        var state = InternalState.Initial().WithAttention(distribution);

        // Assert
        state.AttentionDistribution.Should().HaveCount(2);
        state.AttentionDistribution["task1"].Should().Be(0.7);
    }

    [Fact]
    public void WithMode_ChangesProcessingMode()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var updated = state.WithMode(ProcessingMode.Analytical);

        // Assert
        updated.Mode.Should().Be(ProcessingMode.Analytical);
    }
}

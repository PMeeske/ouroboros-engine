namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class InternalStateTests
{
    [Fact]
    public void Initial_HasDefaultValues()
    {
        var state = InternalState.Initial();

        state.ActiveGoals.Should().BeEmpty();
        state.CurrentFocus.Should().Be("None");
        state.CognitiveLoad.Should().Be(0.0);
        state.EmotionalValence.Should().Be(0.0);
        state.Mode.Should().Be(ProcessingMode.Reactive);
        state.WorkingMemoryItems.Should().BeEmpty();
    }

    [Fact]
    public void WithGoal_AddsGoal()
    {
        var state = InternalState.Initial().WithGoal("Solve problem");
        state.ActiveGoals.Should().Contain("Solve problem");
    }

    [Fact]
    public void WithGoal_IgnoresEmptyString()
    {
        var state = InternalState.Initial().WithGoal("");
        state.ActiveGoals.Should().BeEmpty();
    }

    [Fact]
    public void WithoutGoal_RemovesGoal()
    {
        var state = InternalState.Initial()
            .WithGoal("A").WithGoal("B").WithoutGoal("A");

        state.ActiveGoals.Should().NotContain("A");
        state.ActiveGoals.Should().Contain("B");
    }

    [Fact]
    public void WithFocus_UpdatesFocus()
    {
        var state = InternalState.Initial().WithFocus("analysis");
        state.CurrentFocus.Should().Be("analysis");
    }

    [Fact]
    public void WithFocus_NullDefaultsToNone()
    {
        var state = InternalState.Initial().WithFocus(null!);
        state.CurrentFocus.Should().Be("None");
    }

    [Fact]
    public void WithCognitiveLoad_ClampsValue()
    {
        var state = InternalState.Initial().WithCognitiveLoad(1.5);
        state.CognitiveLoad.Should().Be(1.0);

        state = InternalState.Initial().WithCognitiveLoad(-0.5);
        state.CognitiveLoad.Should().Be(0.0);
    }

    [Fact]
    public void WithValence_ClampsValue()
    {
        var state = InternalState.Initial().WithValence(2.0);
        state.EmotionalValence.Should().Be(1.0);

        state = InternalState.Initial().WithValence(-2.0);
        state.EmotionalValence.Should().Be(-1.0);
    }

    [Fact]
    public void WithWorkingMemoryItem_AddsItem()
    {
        var state = InternalState.Initial().WithWorkingMemoryItem("item1");
        state.WorkingMemoryItems.Should().Contain("item1");
    }

    [Fact]
    public void WithWorkingMemoryItem_IgnoresEmpty()
    {
        var state = InternalState.Initial().WithWorkingMemoryItem("");
        state.WorkingMemoryItems.Should().BeEmpty();
    }

    [Fact]
    public void WithMode_ChangesProcessingMode()
    {
        var state = InternalState.Initial().WithMode(ProcessingMode.Analytical);
        state.Mode.Should().Be(ProcessingMode.Analytical);
    }

    [Fact]
    public void Snapshot_CreatesNewIdAndTimestamp()
    {
        var state = InternalState.Initial();
        var snapshot = state.Snapshot();

        snapshot.Id.Should().NotBe(state.Id);
    }
}

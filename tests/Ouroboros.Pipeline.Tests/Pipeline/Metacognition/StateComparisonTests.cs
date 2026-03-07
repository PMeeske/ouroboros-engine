namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class StateComparisonTests
{
    [Fact]
    public void Create_ComputesDeltas()
    {
        var before = InternalState.Initial().WithCognitiveLoad(0.3);
        var after = InternalState.Initial().WithCognitiveLoad(0.8);

        var comparison = StateComparison.Create(before, after);

        comparison.CognitiveLoadDelta.Should().BeApproximately(0.5, 0.001);
        comparison.CognitiveLoadIncreased.Should().BeTrue();
        comparison.CognitiveLoadDecreased.Should().BeFalse();
    }

    [Fact]
    public void ModeChanged_DetectsModeChange()
    {
        var before = InternalState.Initial().WithMode(ProcessingMode.Reactive);
        var after = InternalState.Initial().WithMode(ProcessingMode.Analytical);

        var comparison = StateComparison.Create(before, after);

        comparison.ModeChanged.Should().BeTrue();
    }

    [Fact]
    public void ModeChanged_FalseWhenSameMode()
    {
        var before = InternalState.Initial();
        var after = InternalState.Initial();

        var comparison = StateComparison.Create(before, after);

        comparison.ModeChanged.Should().BeFalse();
    }

    [Fact]
    public void GoalsAdded_TracksNewGoals()
    {
        var before = InternalState.Initial().WithGoal("A");
        var after = InternalState.Initial().WithGoal("A").WithGoal("B");

        var comparison = StateComparison.Create(before, after);

        comparison.GoalsAdded.Should().Contain("B");
    }

    [Fact]
    public void GoalsRemoved_TracksRemovedGoals()
    {
        var before = InternalState.Initial().WithGoal("A").WithGoal("B");
        var after = InternalState.Initial().WithGoal("A");

        var comparison = StateComparison.Create(before, after);

        comparison.GoalsRemoved.Should().Contain("B");
    }
}

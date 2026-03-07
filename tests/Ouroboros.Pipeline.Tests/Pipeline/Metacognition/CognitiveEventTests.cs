namespace Ouroboros.Tests.Pipeline.Metacognition;

using System.Collections.Immutable;
using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class CognitiveEventTests
{
    [Fact]
    public void Thought_CreatesThoughtEvent()
    {
        var evt = CognitiveEvent.Thought("Deep thought");

        evt.EventType.Should().Be(CognitiveEventType.ThoughtGenerated);
        evt.Description.Should().Be("Deep thought");
        evt.Severity.Should().Be(Severity.Info);
        evt.Context.Should().BeEmpty();
    }

    [Fact]
    public void Decision_CreatesDecisionEvent()
    {
        var evt = CognitiveEvent.Decision("Choose path A");

        evt.EventType.Should().Be(CognitiveEventType.DecisionMade);
        evt.Description.Should().Be("Choose path A");
    }

    [Fact]
    public void Error_CreatesErrorEventWithDefaultWarning()
    {
        var evt = CognitiveEvent.Error("Something went wrong");

        evt.EventType.Should().Be(CognitiveEventType.ErrorDetected);
        evt.Severity.Should().Be(Severity.Warning);
    }

    [Fact]
    public void Error_AcceptsCustomSeverity()
    {
        var evt = CognitiveEvent.Error("Critical failure", Severity.Critical);
        evt.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void Confusion_CreatesConfusionEvent()
    {
        var evt = CognitiveEvent.Confusion("Ambiguous input");

        evt.EventType.Should().Be(CognitiveEventType.ConfusionSensed);
        evt.Severity.Should().Be(Severity.Warning);
    }

    [Fact]
    public void Insight_CreatesInsightEvent()
    {
        var evt = CognitiveEvent.Insight("Pattern recognized");

        evt.EventType.Should().Be(CognitiveEventType.InsightGained);
        evt.Severity.Should().Be(Severity.Info);
    }

    [Fact]
    public void AttentionChange_CreatesAttentionShiftEvent()
    {
        var evt = CognitiveEvent.AttentionChange("Focus shift");
        evt.EventType.Should().Be(CognitiveEventType.AttentionShift);
    }

    [Fact]
    public void GoalStart_CreatesGoalActivatedEvent()
    {
        var evt = CognitiveEvent.GoalStart("Solve problem");
        evt.EventType.Should().Be(CognitiveEventType.GoalActivated);
    }

    [Fact]
    public void GoalEnd_CreatesGoalCompletedEvent()
    {
        var evt = CognitiveEvent.GoalEnd("Problem solved");
        evt.EventType.Should().Be(CognitiveEventType.GoalCompleted);
    }

    [Fact]
    public void UncertaintyDetected_CreatesUncertaintyEvent()
    {
        var evt = CognitiveEvent.UncertaintyDetected("Unknown territory");

        evt.EventType.Should().Be(CognitiveEventType.Uncertainty);
        evt.Severity.Should().Be(Severity.Warning);
    }

    [Fact]
    public void ContradictionDetected_CreatesCriticalEvent()
    {
        var evt = CognitiveEvent.ContradictionDetected("A and not A");

        evt.EventType.Should().Be(CognitiveEventType.Contradiction);
        evt.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void WithContext_AddsContextEntry()
    {
        var evt = CognitiveEvent.Thought("test")
            .WithContext("key", "value");

        evt.Context.Should().ContainKey("key");
    }

    [Fact]
    public void WithMergedContext_MergesAdditionalContext()
    {
        var additional = ImmutableDictionary<string, object>.Empty
            .Add("extra", "data");

        var evt = CognitiveEvent.Thought("test")
            .WithMergedContext(additional);

        evt.Context.Should().ContainKey("extra");
    }
}

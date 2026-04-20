using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Tests.Metacognition;

[Trait("Category", "Unit")]
public sealed class CognitiveEventTests
{
    [Fact]
    public void Thought_CreatesThoughtGeneratedEvent()
    {
        // Act
        var evt = CognitiveEvent.Thought("A new idea");

        // Assert
        evt.Id.Should().NotBeEmpty();
        evt.EventType.Should().Be(CognitiveEventType.ThoughtGenerated);
        evt.Description.Should().Be("A new idea");
        evt.Severity.Should().Be(Severity.Info);
        evt.Context.Should().BeEmpty();
    }

    [Fact]
    public void Thought_WithContext_IncludesContext()
    {
        // Arrange
        var context = ImmutableDictionary<string, object>.Empty.Add("key", "value");

        // Act
        var evt = CognitiveEvent.Thought("idea", context);

        // Assert
        evt.Context.Should().ContainKey("key");
    }

    [Fact]
    public void Decision_CreatesDecisionMadeEvent()
    {
        // Act
        var evt = CognitiveEvent.Decision("Chose option A");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.DecisionMade);
        evt.Severity.Should().Be(Severity.Info);
    }

    [Fact]
    public void Error_CreatesErrorDetectedEvent_WithDefaultWarningSeverity()
    {
        // Act
        var evt = CognitiveEvent.Error("Something went wrong");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.ErrorDetected);
        evt.Severity.Should().Be(Severity.Warning);
    }

    [Fact]
    public void Error_WithCriticalSeverity_SetsSeverityCorrectly()
    {
        // Act
        var evt = CognitiveEvent.Error("Critical error", Severity.Critical);

        // Assert
        evt.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void Confusion_CreatesConfusionSensedEvent()
    {
        // Act
        var evt = CognitiveEvent.Confusion("Confused about task");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.ConfusionSensed);
        evt.Severity.Should().Be(Severity.Warning);
    }

    [Fact]
    public void Insight_CreatesInsightGainedEvent()
    {
        // Act
        var evt = CognitiveEvent.Insight("Found a pattern");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.InsightGained);
        evt.Severity.Should().Be(Severity.Info);
    }

    [Fact]
    public void AttentionChange_CreatesAttentionShiftEvent()
    {
        // Act
        var evt = CognitiveEvent.AttentionChange("Shifted to new topic");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.AttentionShift);
        evt.Severity.Should().Be(Severity.Info);
    }

    [Fact]
    public void GoalStart_CreatesGoalActivatedEvent()
    {
        // Act
        var evt = CognitiveEvent.GoalStart("Complete analysis");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.GoalActivated);
        evt.Severity.Should().Be(Severity.Info);
    }

    [Fact]
    public void GoalEnd_CreatesGoalCompletedEvent()
    {
        // Act
        var evt = CognitiveEvent.GoalEnd("Analysis completed");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.GoalCompleted);
        evt.Severity.Should().Be(Severity.Info);
    }

    [Fact]
    public void UncertaintyDetected_CreatesUncertaintyEvent()
    {
        // Act
        var evt = CognitiveEvent.UncertaintyDetected("High uncertainty");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.Uncertainty);
        evt.Severity.Should().Be(Severity.Warning);
    }

    [Fact]
    public void ContradictionDetected_CreatesContradictionEvent_WithCriticalSeverity()
    {
        // Act
        var evt = CognitiveEvent.ContradictionDetected("Conflicting data");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.Contradiction);
        evt.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void WithContext_AddsContextKeyValue()
    {
        // Arrange
        var evt = CognitiveEvent.Thought("test");

        // Act
        var withCtx = evt.WithContext("source", "test-module");

        // Assert
        withCtx.Context.Should().ContainKey("source");
        withCtx.Context["source"].Should().Be("test-module");
        evt.Context.Should().BeEmpty(); // original unchanged
    }

    [Fact]
    public void WithMergedContext_MergesAdditionalContext()
    {
        // Arrange
        var evt = CognitiveEvent.Thought("test").WithContext("key1", "val1");
        var additional = ImmutableDictionary<string, object>.Empty
            .Add("key2", "val2")
            .Add("key3", "val3");

        // Act
        var merged = evt.WithMergedContext(additional);

        // Assert
        merged.Context.Should().HaveCount(3);
        merged.Context.Should().ContainKey("key1");
        merged.Context.Should().ContainKey("key2");
        merged.Context.Should().ContainKey("key3");
    }

    [Fact]
    public void AllFactoryMethods_GenerateUniqueIds()
    {
        // Act
        var ids = new[]
        {
            CognitiveEvent.Thought("t").Id,
            CognitiveEvent.Decision("d").Id,
            CognitiveEvent.Error("e").Id,
            CognitiveEvent.Confusion("c").Id,
            CognitiveEvent.Insight("i").Id,
        };

        // Assert
        ids.Distinct().Should().HaveCount(ids.Length);
    }
}

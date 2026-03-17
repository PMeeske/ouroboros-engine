using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Tests.Metacognition;

[Trait("Category", "Unit")]
public sealed class CognitiveIntrospectorTests
{
    [Fact]
    public void Constructor_WithDefaultSize_CreatesInstance()
    {
        // Act
        var introspector = new CognitiveIntrospector();

        // Assert - should be able to capture state
        var result = introspector.CaptureState();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNegativeSize_DefaultsTo100()
    {
        // Act
        var introspector = new CognitiveIntrospector(-1);

        // Assert - capture more than default but shouldn't crash
        for (var i = 0; i < 105; i++)
        {
            introspector.CaptureState();
        }

        var history = introspector.GetStateHistory();
        history.IsSuccess.Should().BeTrue();
        history.Value.Should().HaveCount(100); // capped at 100
    }

    [Fact]
    public void CaptureState_ReturnsSnapshotOfCurrentState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        introspector.SetCurrentFocus("Analysis");
        introspector.SetCognitiveLoad(0.7);

        // Act
        var result = introspector.CaptureState();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentFocus.Should().Be("Analysis");
        result.Value.CognitiveLoad.Should().Be(0.7);
    }

    [Fact]
    public void CaptureState_AddsToHistory()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        introspector.CaptureState();
        introspector.CaptureState();

        // Assert
        var history = introspector.GetStateHistory();
        history.IsSuccess.Should().BeTrue();
        history.Value.Should().HaveCount(2);
    }

    [Fact]
    public void CaptureState_TrimsHistoryWhenExceedingMaxSize()
    {
        // Arrange
        var introspector = new CognitiveIntrospector(maxHistorySize: 5);

        // Act
        for (var i = 0; i < 10; i++)
        {
            introspector.CaptureState();
        }

        // Assert
        var history = introspector.GetStateHistory();
        history.IsSuccess.Should().BeTrue();
        history.Value.Should().HaveCount(5);
    }

    [Fact]
    public void Analyze_WithNullState_ReturnsFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.Analyze(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Analyze_WithDefaultState_ReturnsReport()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var state = InternalState.Initial();

        // Act
        var result = introspector.Analyze(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Observations.Should().NotBeEmpty();
        result.Value.StateSnapshot.Should().BeSameAs(state);
    }

    [Fact]
    public void Analyze_WithHighCognitiveLoad_DetectsAnomaly()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var state = InternalState.Initial().WithCognitiveLoad(0.9);

        // Act
        var result = introspector.Analyze(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasAnomalies.Should().BeTrue();
        result.Value.HasRecommendations.Should().BeTrue();
    }

    [Fact]
    public void Analyze_WithExtremeValence_DetectsAnomaly()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var state = InternalState.Initial().WithValence(0.9);

        // Act
        var result = introspector.Analyze(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasAnomalies.Should().BeTrue();
    }

    [Fact]
    public void Analyze_WithFragmentedAttention_DetectsAnomaly()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var attention = ImmutableDictionary<string, double>.Empty
            .Add("a", 0.1).Add("b", 0.1).Add("c", 0.1)
            .Add("d", 0.1).Add("e", 0.1).Add("f", 0.1);
        var state = InternalState.Initial().WithAttention(attention);

        // Act
        var result = introspector.Analyze(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Anomalies.Should().Contain(a => a.Contains("fragmentation"));
    }

    [Fact]
    public void Analyze_WithOverloadedWorkingMemory_DetectsAnomaly()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var state = InternalState.Initial();
        for (var i = 0; i < 10; i++)
        {
            state = state.WithWorkingMemoryItem($"item{i}");
        }

        // Act
        var result = introspector.Analyze(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Anomalies.Should().Contain(a => a.Contains("capacity"));
    }

    [Fact]
    public void Analyze_WithNoGoals_DetectsAnomaly()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var state = InternalState.Initial(); // no goals

        // Act
        var result = introspector.Analyze(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Anomalies.Should().Contain(a => a.Contains("No active goals"));
    }

    [Fact]
    public void Analyze_WithTooManyGoals_DetectsAnomaly()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var state = InternalState.Initial();
        for (var i = 0; i < 7; i++)
        {
            state = state.WithGoal($"Goal {i}");
        }

        // Act
        var result = introspector.Analyze(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Anomalies.Should().Contain(a => a.Contains("Multiple concurrent goals"));
    }

    [Fact]
    public void Analyze_WithReactiveModAndHighLoad_RecommendsAnalyticalMode()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var state = InternalState.Initial()
            .WithMode(ProcessingMode.Reactive)
            .WithCognitiveLoad(0.7);

        // Act
        var result = introspector.Analyze(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Recommendations.Should().Contain(r => r.Contains("analytical mode"));
    }

    [Fact]
    public void Analyze_SelfAssessmentScore_ReflectsStateQuality()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Good state: reflective mode, focused, moderate load, 1-3 goals
        var goodState = InternalState.Initial()
            .WithMode(ProcessingMode.Reflective)
            .WithFocus("Problem solving")
            .WithCognitiveLoad(0.5)
            .WithGoal("Analyze data");

        // Bad state: high load, no focus, many anomalies
        var badState = InternalState.Initial()
            .WithCognitiveLoad(0.95)
            .WithValence(-0.9);

        // Act
        var goodReport = introspector.Analyze(goodState);
        var badReport = introspector.Analyze(badState);

        // Assert
        goodReport.Value.SelfAssessmentScore.Should().BeGreaterThan(badReport.Value.SelfAssessmentScore);
    }

    [Fact]
    public void CompareStates_WithNullBefore_ReturnsFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.CompareStates(null!, InternalState.Initial());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CompareStates_WithNullAfter_ReturnsFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.CompareStates(InternalState.Initial(), null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CompareStates_WithValidStates_ReturnsComparison()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var before = InternalState.Initial().WithCognitiveLoad(0.3);
        var after = InternalState.Initial().WithCognitiveLoad(0.8);

        // Act
        var result = introspector.CompareStates(before, after);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CognitiveLoadDelta.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void IdentifyPatterns_WithNullHistory_ReturnsFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.IdentifyPatterns(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void IdentifyPatterns_WithInsufficientHistory_ReturnsInsufficientMessage()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var history = new[] { InternalState.Initial() };

        // Act
        var result = introspector.IdentifyPatterns(history);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(p => p.Contains("Insufficient"));
    }

    [Fact]
    public void IdentifyPatterns_WithIncreasingLoad_DetectsLoadTrend()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var history = Enumerable.Range(0, 10)
            .Select(i => InternalState.Initial().WithCognitiveLoad(0.1 + i * 0.08))
            .ToList();

        // Act
        var result = introspector.IdentifyPatterns(history);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(p => p.Contains("load") || p.Contains("trend"));
    }

    [Fact]
    public void IdentifyPatterns_WithFrequentModeChanges_DetectsTaskSwitching()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var modes = new[] { ProcessingMode.Analytical, ProcessingMode.Creative,
            ProcessingMode.Reactive, ProcessingMode.Reflective, ProcessingMode.Intuitive };
        var history = Enumerable.Range(0, 10)
            .Select(i => InternalState.Initial().WithMode(modes[i % modes.Length]))
            .ToList();

        // Act
        var result = introspector.IdentifyPatterns(history);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(p => p.Contains("mode") || p.Contains("transition"));
    }

    [Fact]
    public void IdentifyPatterns_WithChronicHighLoad_DetectsPattern()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var history = Enumerable.Range(0, 10)
            .Select(_ => InternalState.Initial().WithCognitiveLoad(0.9))
            .ToList();

        // Act
        var result = introspector.IdentifyPatterns(history);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(p => p.Contains("Chronic high cognitive load"));
    }

    [Fact]
    public void SetCurrentFocus_WithValidFocus_ReturnsSuccess()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetCurrentFocus("Analysis");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify via capture
        var captured = introspector.CaptureState();
        captured.Value.CurrentFocus.Should().Be("Analysis");
    }

    [Fact]
    public void SetCurrentFocus_WithEmptyString_ReturnsFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetCurrentFocus("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddGoal_WithValidGoal_ReturnsSuccess()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.AddGoal("Analyze data");

        // Assert
        result.IsSuccess.Should().BeTrue();

        var state = introspector.CaptureState();
        state.Value.ActiveGoals.Should().Contain("Analyze data");
    }

    [Fact]
    public void AddGoal_WithDuplicateGoal_ReturnsFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        introspector.AddGoal("Analyze data");

        // Act
        var result = introspector.AddGoal("Analyze data");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddGoal_WithEmptyGoal_ReturnsFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.AddGoal("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RemoveGoal_WithExistingGoal_ReturnsSuccess()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        introspector.AddGoal("Task 1");

        // Act
        var result = introspector.RemoveGoal("Task 1");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RemoveGoal_WithNonExistingGoal_ReturnsFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.RemoveGoal("Nonexistent");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RemoveGoal_WithEmptyString_ReturnsFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.RemoveGoal("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetCognitiveLoad_SetsValueInState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetCognitiveLoad(0.75);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var state = introspector.CaptureState();
        state.Value.CognitiveLoad.Should().Be(0.75);
    }

    [Fact]
    public void SetValence_SetsValueInState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetValence(-0.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var state = introspector.CaptureState();
        state.Value.EmotionalValence.Should().Be(-0.5);
    }

    [Fact]
    public void SetMode_SetsProcessingMode()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetMode(ProcessingMode.Analytical);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var state = introspector.CaptureState();
        state.Value.Mode.Should().Be(ProcessingMode.Analytical);
    }

    [Fact]
    public void GetStateHistory_InitiallyEmpty()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var history = introspector.GetStateHistory();

        // Assert
        history.IsSuccess.Should().BeTrue();
        history.Value.Should().BeEmpty();
    }
}

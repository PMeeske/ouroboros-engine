using FluentAssertions;
using Ouroboros.Agent.MetaAI.Executive;
using Xunit;

namespace Ouroboros.Tests.MetaAI.Executive;

[Trait("Category", "Unit")]
public class CognitiveFlexibilityEngineTests
{
    private readonly CognitiveFlexibilityEngine _sut;

    public CognitiveFlexibilityEngineTests()
    {
        _sut = new CognitiveFlexibilityEngine();
    }

    #region EvaluateStrategyAsync Tests

    [Fact]
    public async Task EvaluateStrategyAsync_NullStrategy_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.EvaluateStrategyAsync(null!, [true, false]);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateStrategyAsync_NullOutcomes_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.EvaluateStrategyAsync("strategy", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateStrategyAsync_CancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.EvaluateStrategyAsync("strategy", [true], cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EvaluateStrategyAsync_ThreeConsecutiveFailures_RecommendsShift()
    {
        // Arrange
        var outcomes = new List<bool> { true, false, false, false };

        // Act
        var result = await _sut.EvaluateStrategyAsync("brute-force", outcomes);

        // Assert
        result.ShouldShift.Should().BeTrue();
        result.Reason.Should().Contain("3 consecutive failures");
        result.Reason.Should().Contain("shift recommended");
    }

    [Fact]
    public async Task EvaluateStrategyAsync_TwoConsecutiveFailures_DoesNotRecommendShift()
    {
        // Arrange
        var outcomes = new List<bool> { true, true, false, false };

        // Act
        var result = await _sut.EvaluateStrategyAsync("careful-approach", outcomes);

        // Assert
        result.ShouldShift.Should().BeFalse();
        result.Reason.Should().Contain("continue");
    }

    [Fact]
    public async Task EvaluateStrategyAsync_AllSuccesses_DoesNotRecommendShift()
    {
        // Arrange
        var outcomes = new List<bool> { true, true, true, true };

        // Act
        var result = await _sut.EvaluateStrategyAsync("good-strategy", outcomes);

        // Assert
        result.ShouldShift.Should().BeFalse();
        result.RecommendedStrategy.Should().Be("good-strategy");
    }

    [Fact]
    public async Task EvaluateStrategyAsync_MoreFailures_HigherConfidence()
    {
        // Arrange
        var outcomes = new List<bool> { false, false, false, false, false };

        // Act
        var result = await _sut.EvaluateStrategyAsync("failing-strategy", outcomes);

        // Assert
        result.ShouldShift.Should().BeTrue();
        result.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task EvaluateStrategyAsync_EmptyOutcomes_DoesNotRecommendShift()
    {
        // Arrange
        var outcomes = new List<bool>();

        // Act
        var result = await _sut.EvaluateStrategyAsync("strategy", outcomes);

        // Assert
        result.ShouldShift.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateStrategyAsync_WhenShiftNeeded_RecommendedStrategyIsAlternative()
    {
        // Arrange
        var outcomes = new List<bool> { false, false, false };

        // Act
        var result = await _sut.EvaluateStrategyAsync("bad-strategy", outcomes);

        // Assert
        result.ShouldShift.Should().BeTrue();
        result.RecommendedStrategy.Should().Be("Alternative strategy needed");
    }

    #endregion

    #region GenerateAlternativeStrategiesAsync Tests

    [Fact]
    public async Task GenerateAlternativeStrategiesAsync_NullStrategy_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.GenerateAlternativeStrategiesAsync(null!, "context");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateAlternativeStrategiesAsync_CancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.GenerateAlternativeStrategiesAsync("strategy", "context", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateAlternativeStrategiesAsync_ReturnsSevenAlternatives()
    {
        // Act — 7 SCAMPER operators
        var alternatives = await _sut.GenerateAlternativeStrategiesAsync("brute-force", "complex problem");

        // Assert
        alternatives.Should().HaveCount(7);
    }

    [Fact]
    public async Task GenerateAlternativeStrategiesAsync_AllAlternativesHaveValidProperties()
    {
        // Act
        var alternatives = await _sut.GenerateAlternativeStrategiesAsync("my-strategy", "context");

        // Assert
        foreach (var alt in alternatives)
        {
            alt.Description.Should().NotBeNullOrEmpty();
            alt.ScamperOperator.Should().NotBeNullOrEmpty();
            alt.EstimatedEffectiveness.Should().BeGreaterThanOrEqualTo(0.0);
            alt.EstimatedEffectiveness.Should().BeLessThanOrEqualTo(1.0);
        }
    }

    [Fact]
    public async Task GenerateAlternativeStrategiesAsync_ContainsAllScamperOperators()
    {
        // Act
        var alternatives = await _sut.GenerateAlternativeStrategiesAsync("strategy", "context");
        var operators = alternatives.Select(a => a.ScamperOperator).ToList();

        // Assert
        operators.Should().Contain("Substitute");
        operators.Should().Contain("Combine");
        operators.Should().Contain("Adapt");
        operators.Should().Contain("Modify");
        operators.Should().Contain("Put to another use");
        operators.Should().Contain("Eliminate");
        operators.Should().Contain("Reverse");
    }

    [Fact]
    public async Task GenerateAlternativeStrategiesAsync_SortedByEffectivenessDescending()
    {
        // Act
        var alternatives = await _sut.GenerateAlternativeStrategiesAsync("strategy", "context");

        // Assert
        for (int i = 1; i < alternatives.Count; i++)
        {
            alternatives[i - 1].EstimatedEffectiveness.Should()
                .BeGreaterThanOrEqualTo(alternatives[i].EstimatedEffectiveness);
        }
    }

    [Fact]
    public async Task GenerateAlternativeStrategiesAsync_DescriptionsReferenceOriginalStrategy()
    {
        // Act
        var alternatives = await _sut.GenerateAlternativeStrategiesAsync("brute-force", "context");

        // Assert
        foreach (var alt in alternatives)
        {
            alt.Description.Should().Contain("brute-force");
        }
    }

    #endregion

    #region EstimateTaskSwitchCost Tests

    [Fact]
    public void EstimateTaskSwitchCost_NullFromTask_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.EstimateTaskSwitchCost(null!, "task2");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EstimateTaskSwitchCost_NullToTask_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.EstimateTaskSwitchCost("task1", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EstimateTaskSwitchCost_IdenticalTasks_ReturnsZero()
    {
        // Act
        var cost = _sut.EstimateTaskSwitchCost(
            "analyze data processing pipeline",
            "analyze data processing pipeline");

        // Assert
        cost.Should().Be(0.0);
    }

    [Fact]
    public void EstimateTaskSwitchCost_CompletelyDifferentTasks_ReturnsHigh()
    {
        // Act
        var cost = _sut.EstimateTaskSwitchCost(
            "analyze financial reports for quarterly review",
            "debug kubernetes deployment configuration");

        // Assert
        cost.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void EstimateTaskSwitchCost_SimilarTasks_ReturnsLow()
    {
        // Act
        var cost = _sut.EstimateTaskSwitchCost(
            "analyze data pipeline errors",
            "debug data pipeline issues");

        // Assert
        cost.Should().BeLessThan(0.8);
    }

    [Fact]
    public void EstimateTaskSwitchCost_BothEmpty_ReturnsZero()
    {
        // Act — empty strings produce no keywords (all < 3 chars)
        var cost = _sut.EstimateTaskSwitchCost("", "");

        // Assert
        cost.Should().Be(0.0);
    }

    [Fact]
    public void EstimateTaskSwitchCost_ReturnsBetweenZeroAndOne()
    {
        // Act
        var cost = _sut.EstimateTaskSwitchCost("some task description", "another task context");

        // Assert
        cost.Should().BeGreaterThanOrEqualTo(0.0);
        cost.Should().BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region RecordShiftOutcome and ShiftSuccessRate Tests

    [Fact]
    public void ShiftSuccessRate_NoShifts_ReturnsZero()
    {
        // Act
        var rate = _sut.ShiftSuccessRate;

        // Assert
        rate.Should().Be(0.0);
    }

    [Fact]
    public void RecordShiftOutcome_Successful_IncrementsRate()
    {
        // Act
        _sut.RecordShiftOutcome(wasSuccessful: true);

        // Assert
        _sut.ShiftSuccessRate.Should().Be(1.0);
        _sut.TotalShifts.Should().Be(1);
    }

    [Fact]
    public void RecordShiftOutcome_MixedOutcomes_CorrectRate()
    {
        // Arrange & Act
        _sut.RecordShiftOutcome(wasSuccessful: true);
        _sut.RecordShiftOutcome(wasSuccessful: false);
        _sut.RecordShiftOutcome(wasSuccessful: true);
        _sut.RecordShiftOutcome(wasSuccessful: false);

        // Assert
        _sut.ShiftSuccessRate.Should().Be(0.5);
        _sut.TotalShifts.Should().Be(4);
    }

    [Fact]
    public void RecordShiftOutcome_AllFailures_ReturnsZeroRate()
    {
        // Act
        _sut.RecordShiftOutcome(wasSuccessful: false);
        _sut.RecordShiftOutcome(wasSuccessful: false);

        // Assert
        _sut.ShiftSuccessRate.Should().Be(0.0);
    }

    #endregion

    #region AlternativeStrategy Record Tests

    [Fact]
    public void AlternativeStrategy_PropertiesSetCorrectly()
    {
        // Act
        var strategy = new AlternativeStrategy("Replace core component", "Substitute", 0.75);

        // Assert
        strategy.Description.Should().Be("Replace core component");
        strategy.ScamperOperator.Should().Be("Substitute");
        strategy.EstimatedEffectiveness.Should().Be(0.75);
    }

    #endregion
}

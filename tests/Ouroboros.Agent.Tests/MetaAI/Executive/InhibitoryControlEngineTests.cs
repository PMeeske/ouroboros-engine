using FluentAssertions;
using Ouroboros.Agent.MetaAI.Executive;
using Xunit;

namespace Ouroboros.Tests.MetaAI.Executive;

[Trait("Category", "Unit")]
public class InhibitoryControlEngineTests
{
    private readonly InhibitoryControlEngine _sut;

    public InhibitoryControlEngineTests()
    {
        _sut = new InhibitoryControlEngine();
    }

    #region EvaluateResponseInhibitionAsync Tests

    [Fact]
    public async Task EvaluateResponseInhibitionAsync_NullAction_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.EvaluateResponseInhibitionAsync(null!, "context");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateResponseInhibitionAsync_CancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.EvaluateResponseInhibitionAsync("action", "context", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EvaluateResponseInhibitionAsync_UncertainContext_InhibitsLowUrgencyAction()
    {
        // Arrange — low urgency action in uncertain context
        // Act
        var result = await _sut.EvaluateResponseInhibitionAsync(
            "optional low priority task", "This is an uncertain situation");

        // Assert
        result.ShouldInhibit.Should().BeTrue();
        result.Reason.Should().Contain("Inhibited");
        result.SuggestedDelay.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task EvaluateResponseInhibitionAsync_SafeContext_AllowsAction()
    {
        // Arrange — normal action in safe context
        // Act
        var result = await _sut.EvaluateResponseInhibitionAsync(
            "process the data", "Everything is normal and stable");

        // Assert
        result.ShouldInhibit.Should().BeFalse();
        result.Reason.Should().Contain("Allowed");
    }

    [Fact]
    public async Task EvaluateResponseInhibitionAsync_CriticalUrgentAction_AllowedEvenInRiskyContext()
    {
        // Arrange — critical+urgent action overrides inhibition
        // Act
        var result = await _sut.EvaluateResponseInhibitionAsync(
            "critical urgent safety emergency response",
            "risky uncertain situation");

        // Assert
        result.ShouldInhibit.Should().BeFalse();
        result.Reason.Should().Contain("Allowed");
    }

    [Fact]
    public async Task EvaluateResponseInhibitionAsync_RiskyContext_TriggersDeliberation()
    {
        // Arrange
        // Act
        var result = await _sut.EvaluateResponseInhibitionAsync(
            "optional maintenance task", "This is risky and uncertain");

        // Assert
        result.ShouldInhibit.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateResponseInhibitionAsync_NovelContext_TriggersDeliberation()
    {
        // Act
        var result = await _sut.EvaluateResponseInhibitionAsync(
            "optional low priority action", "novel situation requiring careful assessment");

        // Assert
        result.ShouldInhibit.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateResponseInhibitionAsync_IrreversibleContext_TriggersDeliberation()
    {
        // Act
        var result = await _sut.EvaluateResponseInhibitionAsync(
            "optional action", "irreversible consequences possible");

        // Assert
        result.ShouldInhibit.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateResponseInhibitionAsync_IncrementsEvaluationCount()
    {
        // Arrange
        var metricsBefore = _sut.GetMetrics();

        // Act
        await _sut.EvaluateResponseInhibitionAsync("action", "context");

        // Assert
        var metricsAfter = _sut.GetMetrics();
        metricsAfter.TotalEvaluations.Should().Be(metricsBefore.TotalEvaluations + 1);
    }

    [Fact]
    public async Task EvaluateResponseInhibitionAsync_InhibitedResult_HasPositiveDelay()
    {
        // Act
        var result = await _sut.EvaluateResponseInhibitionAsync(
            "optional task", "uncertain context");

        // Assert
        if (result.ShouldInhibit)
        {
            result.SuggestedDelay.Should().BeGreaterThan(TimeSpan.Zero);
        }
    }

    [Fact]
    public async Task EvaluateResponseInhibitionAsync_AllowedResult_HasZeroDelay()
    {
        // Act
        var result = await _sut.EvaluateResponseInhibitionAsync(
            "critical urgent action", "safe context");

        // Assert
        if (!result.ShouldInhibit)
        {
            result.SuggestedDelay.Should().Be(TimeSpan.Zero);
        }
    }

    #endregion

    #region ShouldSuppressAsync Tests

    [Fact]
    public async Task ShouldSuppressAsync_NullImpulse_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.ShouldSuppressAsync(null!, 0.5);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ShouldSuppressAsync_CancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.ShouldSuppressAsync("impulse", 0.5, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ShouldSuppressAsync_LowUrgency_ReturnsTrueForSuppression()
    {
        // Arrange — default inhibition strength is 0.5
        // Act
        var result = await _sut.ShouldSuppressAsync("do something", 0.2);

        // Assert — 0.2 < 0.5 so should suppress
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSuppressAsync_HighUrgency_ReturnsFalse()
    {
        // Act
        var result = await _sut.ShouldSuppressAsync("urgent action", 0.9);

        // Assert — 0.9 > 0.5 so should not suppress
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldSuppressAsync_IncrementsEvaluationCount()
    {
        // Arrange
        var metricsBefore = _sut.GetMetrics();

        // Act
        await _sut.ShouldSuppressAsync("impulse", 0.5);

        // Assert
        var metricsAfter = _sut.GetMetrics();
        metricsAfter.TotalEvaluations.Should().Be(metricsBefore.TotalEvaluations + 1);
    }

    #endregion

    #region RecordOutcome Tests

    [Fact]
    public void RecordOutcome_CorrectInhibition_IncreasesInhibitionStrength()
    {
        // Arrange
        var strengthBefore = _sut.InhibitionStrength;

        // Act
        _sut.RecordOutcome(wasInhibited: true, wasCorrectDecision: true);

        // Assert
        _sut.InhibitionStrength.Should().BeGreaterThan(strengthBefore);
    }

    [Fact]
    public void RecordOutcome_FalseAlarm_DecreasesInhibitionStrength()
    {
        // Arrange
        var strengthBefore = _sut.InhibitionStrength;

        // Act
        _sut.RecordOutcome(wasInhibited: true, wasCorrectDecision: false);

        // Assert
        _sut.InhibitionStrength.Should().BeLessThan(strengthBefore);
    }

    [Fact]
    public void RecordOutcome_Miss_RecordedInMetrics()
    {
        // Act
        _sut.RecordOutcome(wasInhibited: false, wasCorrectDecision: false);

        // Assert
        var metrics = _sut.GetMetrics();
        metrics.Misses.Should().Be(1);
    }

    [Fact]
    public void RecordOutcome_InhibitionStrengthClampedToRange()
    {
        // Act — many false alarms to push strength down
        for (int i = 0; i < 100; i++)
        {
            _sut.RecordOutcome(wasInhibited: true, wasCorrectDecision: false);
        }

        // Assert
        _sut.InhibitionStrength.Should().BeGreaterThanOrEqualTo(0.1);
        _sut.InhibitionStrength.Should().BeLessThanOrEqualTo(0.95);
    }

    [Fact]
    public void RecordOutcome_ManyCorrectInhibitions_StrengthClampedToMax()
    {
        // Act — many correct inhibitions to push strength up
        for (int i = 0; i < 100; i++)
        {
            _sut.RecordOutcome(wasInhibited: true, wasCorrectDecision: true);
        }

        // Assert
        _sut.InhibitionStrength.Should().BeLessThanOrEqualTo(0.95);
    }

    #endregion

    #region GetMetrics Tests

    [Fact]
    public void GetMetrics_InitialState_AllZeros()
    {
        // Act
        var metrics = _sut.GetMetrics();

        // Assert
        metrics.TotalEvaluations.Should().Be(0);
        metrics.CorrectInhibitions.Should().Be(0);
        metrics.FalseAlarms.Should().Be(0);
        metrics.Misses.Should().Be(0);
        metrics.Accuracy.Should().Be(0.0);
    }

    [Fact]
    public void GetMetrics_AfterOutcomes_ReturnsCorrectCounts()
    {
        // Arrange — simulate evaluations to increment TotalEvaluations via EvaluateResponseInhibitionAsync
        _sut.RecordOutcome(wasInhibited: true, wasCorrectDecision: true);
        _sut.RecordOutcome(wasInhibited: true, wasCorrectDecision: false);
        _sut.RecordOutcome(wasInhibited: false, wasCorrectDecision: false);

        // Act
        var metrics = _sut.GetMetrics();

        // Assert
        metrics.CorrectInhibitions.Should().Be(1);
        metrics.FalseAlarms.Should().Be(1);
        metrics.Misses.Should().Be(1);
    }

    #endregion

    #region InhibitionStrength Tests

    [Fact]
    public void InhibitionStrength_InitialValue_IsHalf()
    {
        // Assert
        _sut.InhibitionStrength.Should().Be(0.5);
    }

    #endregion

    #region InhibitionType Enum Tests

    [Theory]
    [InlineData(InhibitionType.ResponseInhibition)]
    [InlineData(InhibitionType.InterferenceControl)]
    [InlineData(InhibitionType.ImpulseModulation)]
    public void InhibitionType_AllValuesAreDefined(InhibitionType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }

    [Fact]
    public void InhibitionType_HasThreeValues()
    {
        Enum.GetValues<InhibitionType>().Should().HaveCount(3);
    }

    #endregion

    #region InhibitionMetrics Record Tests

    [Fact]
    public void InhibitionMetrics_PropertiesSetCorrectly()
    {
        // Act
        var metrics = new InhibitionMetrics(100, 70, 10, 20, 0.7);

        // Assert
        metrics.TotalEvaluations.Should().Be(100);
        metrics.CorrectInhibitions.Should().Be(70);
        metrics.FalseAlarms.Should().Be(10);
        metrics.Misses.Should().Be(20);
        metrics.Accuracy.Should().Be(0.7);
    }

    #endregion
}

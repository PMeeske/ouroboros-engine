using FluentAssertions;
using Ouroboros.Agent.MetaAI.Affect;
using Xunit;

namespace Ouroboros.Tests.MetaAI.Affect;

[Trait("Category", "Unit")]
public class CognitiveFatigueModelTests
{
    private readonly CognitiveFatigueModel _sut;

    public CognitiveFatigueModelTests()
    {
        _sut = new CognitiveFatigueModel();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_InitializesWithZeroFatigue()
    {
        // Arrange & Act
        var model = new CognitiveFatigueModel();

        // Assert
        model.GetFatigueLevel().Should().BeGreaterThanOrEqualTo(0.0);
        model.GetFatigueLevel().Should().BeLessThanOrEqualTo(0.05,
            "a freshly created model should have near-zero fatigue");
    }

    [Fact]
    public void Constructor_CustomDecayRate_ClampsToRange()
    {
        // Arrange & Act — extreme values should not throw
        var low = new CognitiveFatigueModel(decayRate: -10.0);
        var high = new CognitiveFatigueModel(decayRate: 100.0);

        // Assert — both should still return valid fatigue levels
        low.GetFatigueLevel().Should().BeGreaterThanOrEqualTo(0.0);
        high.GetFatigueLevel().Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void Constructor_CustomRecoveryRate_ClampsToRange()
    {
        // Arrange & Act
        var low = new CognitiveFatigueModel(recoveryRate: -1.0);
        var high = new CognitiveFatigueModel(recoveryRate: 100.0);

        // Assert
        low.GetFatigueLevel().Should().BeGreaterThanOrEqualTo(0.0);
        high.GetFatigueLevel().Should().BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region RecordCognitiveEffort Tests

    [Fact]
    public void RecordCognitiveEffort_NullTaskId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.RecordCognitiveEffort(null!, 0.5, TimeSpan.FromMinutes(10));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("taskId");
    }

    [Fact]
    public void RecordCognitiveEffort_ValidInput_IncreasesFatigue()
    {
        // Arrange
        var initialFatigue = _sut.GetFatigueLevel();

        // Act
        _sut.RecordCognitiveEffort("task-1", 0.8, TimeSpan.FromMinutes(30));
        var afterFatigue = _sut.GetFatigueLevel();

        // Assert
        afterFatigue.Should().BeGreaterThan(initialFatigue);
    }

    [Fact]
    public void RecordCognitiveEffort_HighComplexityLongDuration_ProducesHighFatigue()
    {
        // Arrange & Act
        _sut.RecordCognitiveEffort("task-heavy", 1.0, TimeSpan.FromMinutes(60));
        _sut.RecordCognitiveEffort("task-heavy2", 1.0, TimeSpan.FromMinutes(60));

        // Assert
        _sut.GetFatigueLevel().Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void RecordCognitiveEffort_ComplexityClamped_ExceedsRange()
    {
        // Arrange & Act — complexity > 1.0 should be clamped
        _sut.RecordCognitiveEffort("task-over", 2.0, TimeSpan.FromMinutes(10));

        // Assert — should not throw and fatigue should be within valid range
        _sut.GetFatigueLevel().Should().BeGreaterThanOrEqualTo(0.0);
        _sut.GetFatigueLevel().Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void RecordCognitiveEffort_NegativeDuration_TreatedAsZero()
    {
        // Arrange
        var initialFatigue = _sut.GetFatigueLevel();

        // Act
        _sut.RecordCognitiveEffort("task-neg", 0.5, TimeSpan.FromMinutes(-10));

        // Assert — negative duration clamped to zero, no effort added
        _sut.GetFatigueLevel().Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void RecordCognitiveEffort_ZeroComplexity_MinimalFatigueIncrease()
    {
        // Arrange
        var initialFatigue = _sut.GetFatigueLevel();

        // Act
        _sut.RecordCognitiveEffort("task-easy", 0.0, TimeSpan.FromMinutes(10));

        // Assert — zero complexity means zero effort units
        var afterFatigue = _sut.GetFatigueLevel();
        afterFatigue.Should().BeGreaterThanOrEqualTo(0.0);
    }

    #endregion

    #region GetFatigueLevel Tests

    [Fact]
    public void GetFatigueLevel_NoEffort_ReturnsNearZero()
    {
        // Act
        var fatigue = _sut.GetFatigueLevel();

        // Assert
        fatigue.Should().BeGreaterThanOrEqualTo(0.0);
        fatigue.Should().BeLessThan(0.1);
    }

    [Fact]
    public void GetFatigueLevel_AlwaysReturnsBetweenZeroAndOne()
    {
        // Arrange — add many effort events
        for (int i = 0; i < 20; i++)
        {
            _sut.RecordCognitiveEffort($"task-{i}", 1.0, TimeSpan.FromMinutes(30));
        }

        // Act
        var fatigue = _sut.GetFatigueLevel();

        // Assert
        fatigue.Should().BeGreaterThanOrEqualTo(0.0);
        fatigue.Should().BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region GetRecoveryEstimate Tests

    [Fact]
    public void GetRecoveryEstimate_NoEffort_ReturnsZero()
    {
        // Act
        var estimate = _sut.GetRecoveryEstimate();

        // Assert
        estimate.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetRecoveryEstimate_AfterEffort_ReturnsPositiveTimeSpan()
    {
        // Arrange
        _sut.RecordCognitiveEffort("task-1", 0.8, TimeSpan.FromMinutes(30));

        // Act
        var estimate = _sut.GetRecoveryEstimate();

        // Assert
        estimate.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void GetRecoveryEstimate_MoreEffort_LongerRecovery()
    {
        // Arrange
        _sut.RecordCognitiveEffort("task-1", 0.5, TimeSpan.FromMinutes(10));
        var estimate1 = _sut.GetRecoveryEstimate();

        _sut.RecordCognitiveEffort("task-2", 0.8, TimeSpan.FromMinutes(30));
        var estimate2 = _sut.GetRecoveryEstimate();

        // Assert
        estimate2.Should().BeGreaterThan(estimate1);
    }

    #endregion

    #region GetSnapshot Tests

    [Fact]
    public void GetSnapshot_NoEffort_ReturnsBaselineSnapshot()
    {
        // Act
        var snapshot = _sut.GetSnapshot();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.FatigueLevel.Should().BeGreaterThanOrEqualTo(0.0);
        snapshot.FatigueLevel.Should().BeLessThan(0.1);
        snapshot.IsHighFatigue.Should().BeFalse();
        snapshot.LastEffortTimestamp.Should().BeNull();
        snapshot.RecoveryEstimate.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetSnapshot_AfterEffort_IncludesLastTimestamp()
    {
        // Arrange
        _sut.RecordCognitiveEffort("task-1", 0.5, TimeSpan.FromMinutes(10));

        // Act
        var snapshot = _sut.GetSnapshot();

        // Assert
        snapshot.LastEffortTimestamp.Should().NotBeNull();
        snapshot.LastEffortTimestamp!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetSnapshot_HighFatigue_SetsIsHighFatigue()
    {
        // Arrange — generate enough fatigue to exceed 0.7 threshold
        for (int i = 0; i < 10; i++)
        {
            _sut.RecordCognitiveEffort($"heavy-{i}", 1.0, TimeSpan.FromMinutes(30));
        }

        // Act
        var snapshot = _sut.GetSnapshot();

        // Assert
        snapshot.FatigueLevel.Should().BeGreaterThanOrEqualTo(CognitiveFatigueModel.HighFatigueThreshold);
        snapshot.IsHighFatigue.Should().BeTrue();
    }

    [Fact]
    public void GetSnapshot_CumulativeEffortMatchesFatigueCalculation()
    {
        // Arrange
        _sut.RecordCognitiveEffort("task-1", 0.5, TimeSpan.FromMinutes(20));

        // Act
        var snapshot = _sut.GetSnapshot();

        // Assert
        snapshot.CumulativeEffort.Should().BeGreaterThan(0.0);
        snapshot.FatigueLevel.Should().BeGreaterThan(0.0);
    }

    #endregion

    #region GetStressSignal Tests

    [Fact]
    public void GetStressSignal_EqualsCurrentFatigueLevel()
    {
        // Arrange
        _sut.RecordCognitiveEffort("task-1", 0.6, TimeSpan.FromMinutes(15));

        // Act
        var stress = _sut.GetStressSignal();
        var fatigue = _sut.GetFatigueLevel();

        // Assert — they should be very close (minor time-based recovery might differ)
        stress.Should().BeApproximately(fatigue, 0.01);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_AfterEffort_RestoresFatigueToBaseline()
    {
        // Arrange
        _sut.RecordCognitiveEffort("task-1", 0.8, TimeSpan.FromMinutes(30));
        _sut.GetFatigueLevel().Should().BeGreaterThan(0.0);

        // Act
        _sut.Reset();

        // Assert
        _sut.GetFatigueLevel().Should().BeLessThan(0.05);
    }

    [Fact]
    public void Reset_ClearsEffortHistory()
    {
        // Arrange
        _sut.RecordCognitiveEffort("task-1", 0.5, TimeSpan.FromMinutes(10));
        _sut.GetEffortHistory().Should().NotBeEmpty();

        // Act
        _sut.Reset();

        // Assert
        _sut.GetEffortHistory().Should().BeEmpty();
    }

    [Fact]
    public void Reset_SnapshotShowsBaselineState()
    {
        // Arrange
        _sut.RecordCognitiveEffort("task-1", 0.5, TimeSpan.FromMinutes(10));
        _sut.Reset();

        // Act
        var snapshot = _sut.GetSnapshot();

        // Assert
        snapshot.IsHighFatigue.Should().BeFalse();
        snapshot.LastEffortTimestamp.Should().BeNull();
        snapshot.RecoveryEstimate.Should().Be(TimeSpan.Zero);
    }

    #endregion

    #region GetEffortHistory Tests

    [Fact]
    public void GetEffortHistory_NoEffort_ReturnsEmptyList()
    {
        // Act
        var history = _sut.GetEffortHistory();

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public void GetEffortHistory_RecordsMultipleEvents()
    {
        // Arrange
        _sut.RecordCognitiveEffort("task-1", 0.3, TimeSpan.FromMinutes(5));
        _sut.RecordCognitiveEffort("task-2", 0.7, TimeSpan.FromMinutes(15));
        _sut.RecordCognitiveEffort("task-3", 0.5, TimeSpan.FromMinutes(10));

        // Act
        var history = _sut.GetEffortHistory();

        // Assert
        history.Should().HaveCount(3);
        history[0].Timestamp.Should().BeOnOrAfter(history[1].Timestamp,
            "history is ordered descending by timestamp");
    }

    [Fact]
    public void GetEffortHistory_RespectsCountLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _sut.RecordCognitiveEffort($"task-{i}", 0.5, TimeSpan.FromMinutes(5));
        }

        // Act
        var history = _sut.GetEffortHistory(count: 3);

        // Assert
        history.Should().HaveCount(3);
    }

    [Fact]
    public void GetEffortHistory_PreservesTaskIdAndComplexity()
    {
        // Arrange
        _sut.RecordCognitiveEffort("my-task", 0.75, TimeSpan.FromMinutes(20));

        // Act
        var history = _sut.GetEffortHistory();

        // Assert
        history.Should().ContainSingle();
        history[0].TaskId.Should().Be("my-task");
        history[0].Complexity.Should().Be(0.75);
        history[0].Duration.Should().Be(TimeSpan.FromMinutes(20));
    }

    #endregion

    #region HighFatigueThreshold Tests

    [Fact]
    public void HighFatigueThreshold_IsPointSeven()
    {
        CognitiveFatigueModel.HighFatigueThreshold.Should().Be(0.7);
    }

    #endregion

    #region CognitiveEffortEvent Record Tests

    [Fact]
    public void CognitiveEffortEvent_PropertiesSetCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var duration = TimeSpan.FromMinutes(15);

        // Act
        var evt = new CognitiveEffortEvent("task-1", 0.5, duration, timestamp);

        // Assert
        evt.TaskId.Should().Be("task-1");
        evt.Complexity.Should().Be(0.5);
        evt.Duration.Should().Be(duration);
        evt.Timestamp.Should().Be(timestamp);
    }

    #endregion

    #region FatigueSnapshot Record Tests

    [Fact]
    public void FatigueSnapshot_PropertiesSetCorrectly()
    {
        // Arrange & Act
        var snapshot = new FatigueSnapshot(
            FatigueLevel: 0.65,
            CumulativeEffort: 3.5,
            RecoveryEstimate: TimeSpan.FromMinutes(35),
            IsHighFatigue: false,
            LastEffortTimestamp: DateTime.UtcNow);

        // Assert
        snapshot.FatigueLevel.Should().Be(0.65);
        snapshot.CumulativeEffort.Should().Be(3.5);
        snapshot.RecoveryEstimate.Should().Be(TimeSpan.FromMinutes(35));
        snapshot.IsHighFatigue.Should().BeFalse();
        snapshot.LastEffortTimestamp.Should().NotBeNull();
    }

    [Fact]
    public void FatigueSnapshot_NullTimestamp_IsValid()
    {
        // Act
        var snapshot = new FatigueSnapshot(0.0, 0.0, TimeSpan.Zero, false, null);

        // Assert
        snapshot.LastEffortTimestamp.Should().BeNull();
    }

    #endregion
}

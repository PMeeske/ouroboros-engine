using FluentAssertions;
using Ouroboros.Agent.MetaAI.Executive;
using Xunit;

namespace Ouroboros.Tests.MetaAI.Executive;

[Trait("Category", "Unit")]
public class AttentionControllerTests
{
    private readonly AttentionController _sut;

    public AttentionControllerTests()
    {
        _sut = new AttentionController();
    }

    #region AllocateAttentionAsync Tests

    [Fact]
    public async Task AllocateAttentionAsync_NullTargets_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.AllocateAttentionAsync(null!, AttentionMode.Normal);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AllocateAttentionAsync_CancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var targets = new List<AttentionTarget>();

        // Act
        var act = () => _sut.AllocateAttentionAsync(targets, AttentionMode.Normal, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AllocateAttentionAsync_EmptyTargets_ReturnsEmptyAllocation()
    {
        // Arrange
        var targets = new List<AttentionTarget>();

        // Act
        var result = await _sut.AllocateAttentionAsync(targets, AttentionMode.Normal);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AllocateAttentionAsync_NormalMode_LimitsToSevenTargets()
    {
        // Arrange — 10 targets, should be capped at 7 (Miller's number)
        var targets = Enumerable.Range(1, 10)
            .Select(i => new AttentionTarget($"t-{i}", $"Target {i}", 0.5, AttentionSource.Exogenous))
            .ToList();

        // Act
        var result = await _sut.AllocateAttentionAsync(targets, AttentionMode.Normal);

        // Assert
        result.UsedCapacity.Should().BeLessThanOrEqualTo(7);
    }

    [Fact]
    public async Task AllocateAttentionAsync_FocusedMode_LimitsToFiveTargets()
    {
        // Arrange — 10 targets, focused mode = 7 - 2 = 5
        var targets = Enumerable.Range(1, 10)
            .Select(i => new AttentionTarget($"t-{i}", $"Target {i}", 0.5, AttentionSource.Exogenous))
            .ToList();

        // Act
        var result = await _sut.AllocateAttentionAsync(targets, AttentionMode.Focused);

        // Assert
        result.UsedCapacity.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task AllocateAttentionAsync_ScanningMode_AllowsUpToNineTargets()
    {
        // Arrange — 10 targets, scanning = 7 + 2 = 9
        var targets = Enumerable.Range(1, 10)
            .Select(i => new AttentionTarget($"t-{i}", $"Target {i}", 0.5, AttentionSource.Exogenous))
            .ToList();

        // Act
        var result = await _sut.AllocateAttentionAsync(targets, AttentionMode.Scanning);

        // Assert
        result.UsedCapacity.Should().BeLessThanOrEqualTo(9);
    }

    [Fact]
    public async Task AllocateAttentionAsync_EndogenousTargets_ReceivePriorityBoost()
    {
        // Arrange
        var targets = new List<AttentionTarget>
        {
            new("exo", "Exogenous target", 0.5, AttentionSource.Exogenous),
            new("endo", "Endogenous target", 0.5, AttentionSource.Endogenous)
        };

        // Act
        var result = await _sut.AllocateAttentionAsync(targets, AttentionMode.Normal);

        // Assert — endogenous target should have higher allocation due to 20% boost
        result.Allocations["endo"].Should().BeGreaterThan(result.Allocations["exo"]);
    }

    [Fact]
    public async Task AllocateAttentionAsync_SelectsHighestPriorityTargets()
    {
        // Arrange — more targets than capacity
        var targets = new List<AttentionTarget>
        {
            new("low", "Low priority", 0.1, AttentionSource.Exogenous),
            new("high", "High priority", 0.9, AttentionSource.Exogenous),
            new("med", "Medium priority", 0.5, AttentionSource.Exogenous),
        };

        // Act
        var result = await _sut.AllocateAttentionAsync(targets, AttentionMode.Normal);

        // Assert — all 3 fit within capacity of 7
        result.Allocations.Should().ContainKey("high");
        result.Allocations.Should().ContainKey("med");
        result.Allocations.Should().ContainKey("low");
    }

    [Fact]
    public async Task AllocateAttentionAsync_FewerTargetsThanCapacity_AllocatesAll()
    {
        // Arrange
        var targets = new List<AttentionTarget>
        {
            new("t1", "Target 1", 0.8, AttentionSource.Exogenous),
            new("t2", "Target 2", 0.6, AttentionSource.Exogenous),
        };

        // Act
        var result = await _sut.AllocateAttentionAsync(targets, AttentionMode.Normal);

        // Assert
        result.Allocations.Should().HaveCount(2);
        result.UsedCapacity.Should().Be(2);
    }

    #endregion

    #region RecordAttentionCapture Tests

    [Fact]
    public void RecordAttentionCapture_NullStimulusId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.RecordAttentionCapture(null!, 0.5, AttentionSource.Exogenous);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordAttentionCapture_ValidInput_IncrementsCount()
    {
        // Arrange
        var initialCount = _sut.CaptureEventCount;

        // Act
        _sut.RecordAttentionCapture("stimulus-1", 0.7, AttentionSource.Exogenous);

        // Assert
        _sut.CaptureEventCount.Should().Be(initialCount + 1);
    }

    [Fact]
    public void RecordAttentionCapture_MultipleCalls_TracksAllEvents()
    {
        // Act
        _sut.RecordAttentionCapture("s1", 0.3, AttentionSource.Endogenous);
        _sut.RecordAttentionCapture("s2", 0.7, AttentionSource.Exogenous);
        _sut.RecordAttentionCapture("s3", 0.5, AttentionSource.Endogenous);

        // Assert
        _sut.CaptureEventCount.Should().Be(3);
    }

    #endregion

    #region GetSustainedAttentionQuality Tests

    [Fact]
    public void GetSustainedAttentionQuality_InitialState_IsNearOne()
    {
        // Act — immediately after creation, fatigue should be minimal
        var quality = _sut.GetSustainedAttentionQuality();

        // Assert
        quality.Should().BeGreaterThan(0.9);
        quality.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void GetSustainedAttentionQuality_AlwaysBetweenZeroAndOne()
    {
        // Act
        var quality = _sut.GetSustainedAttentionQuality();

        // Assert
        quality.Should().BeGreaterThanOrEqualTo(0.0);
        quality.Should().BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region FatigueLevel Tests

    [Fact]
    public void FatigueLevel_InitialState_IsNearZero()
    {
        // Act
        var fatigue = _sut.FatigueLevel;

        // Assert
        fatigue.Should().BeGreaterThanOrEqualTo(0.0);
        fatigue.Should().BeLessThan(0.1);
    }

    [Fact]
    public void FatigueLevel_AlwaysBetweenZeroAndOne()
    {
        // Act
        var fatigue = _sut.FatigueLevel;

        // Assert
        fatigue.Should().BeGreaterThanOrEqualTo(0.0);
        fatigue.Should().BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region BeginRest / EndRest Tests

    [Fact]
    public void BeginRest_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _sut.BeginRest();
        act.Should().NotThrow();
    }

    [Fact]
    public void EndRest_WithoutBeginRest_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _sut.EndRest();
        act.Should().NotThrow();
    }

    [Fact]
    public void EndRest_AfterBeginRest_ResetsTaskStartTime()
    {
        // Arrange
        _sut.BeginRest();

        // Act
        _sut.EndRest();

        // Assert — fatigue should still be valid
        _sut.FatigueLevel.Should().BeGreaterThanOrEqualTo(0.0);
        _sut.FatigueLevel.Should().BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region AttentionCaptureEvent Record Tests

    [Fact]
    public void AttentionCaptureEvent_PropertiesSetCorrectly()
    {
        // Arrange & Act
        var timestamp = DateTime.UtcNow;
        var evt = new AttentionCaptureEvent("stimulus-1", 0.75, AttentionSource.Endogenous, timestamp);

        // Assert
        evt.StimulusId.Should().Be("stimulus-1");
        evt.Salience.Should().Be(0.75);
        evt.Source.Should().Be(AttentionSource.Endogenous);
        evt.Timestamp.Should().Be(timestamp);
    }

    #endregion
}

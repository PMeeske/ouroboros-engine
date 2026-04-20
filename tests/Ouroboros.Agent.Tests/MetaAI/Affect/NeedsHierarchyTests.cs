// <copyright file="NeedsHierarchyTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.Affect;

namespace Ouroboros.Agent.Tests.MetaAI.Affect;

/// <summary>
/// Unit tests for <see cref="NeedsHierarchy"/>.
/// </summary>
[Trait("Category", "Unit")]
public class NeedsHierarchyTests
{
    private readonly NeedsHierarchy _sut = new();

    // --- GetMostUrgentNeed ---

    [Fact]
    public void GetMostUrgentNeed_DefaultState_ReturnsSelfActualizationAsLowest()
    {
        // Arrange — defaults: OpStability=0.8, Safety=0.7, Social=0.5, Recognition=0.4, SelfActualization=0.3
        // None are below 0.3 blocking threshold, so the lowest satisfaction wins

        // Act
        var result = _sut.GetMostUrgentNeed();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Level.Should().Be(NeedLevel.SelfActualization);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public void GetMostUrgentNeed_WhenNeedIsBlocking_ReturnsBlockingNeed()
    {
        // Arrange — drop OperationalStability below threshold
        _sut.SetNeedSatisfaction(NeedLevel.OperationalStability, 0.1, "system degradation");

        // Act
        var result = _sut.GetMostUrgentNeed();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Level.Should().Be(NeedLevel.OperationalStability);
        result.Value.IsBlocking.Should().BeTrue();
    }

    // --- RecordNeedSatisfaction ---

    [Fact]
    public void RecordNeedSatisfaction_NullCause_ThrowsArgumentNullException()
    {
        var act = () => _sut.RecordNeedSatisfaction(NeedLevel.Safety, 0.1, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordNeedSatisfaction_PositiveDelta_IncreasesSatisfaction()
    {
        // Arrange
        var before = _sut.GetSatisfaction(NeedLevel.Safety);

        // Act
        _sut.RecordNeedSatisfaction(NeedLevel.Safety, 0.2, "security improvement");

        // Assert
        var after = _sut.GetSatisfaction(NeedLevel.Safety);
        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public void RecordNeedSatisfaction_NegativeDelta_DecreasesSatisfaction()
    {
        // Arrange
        var before = _sut.GetSatisfaction(NeedLevel.Safety);

        // Act
        _sut.RecordNeedSatisfaction(NeedLevel.Safety, -0.3, "security incident");

        // Assert
        var after = _sut.GetSatisfaction(NeedLevel.Safety);
        after.Should().BeLessThan(before);
    }

    [Fact]
    public void RecordNeedSatisfaction_ClampsToRange()
    {
        // Act — push above 1.0
        _sut.RecordNeedSatisfaction(NeedLevel.OperationalStability, 5.0, "huge boost");

        // Assert
        _sut.GetSatisfaction(NeedLevel.OperationalStability).Should().Be(1.0);
    }

    [Fact]
    public void RecordNeedSatisfaction_ClampsToZero()
    {
        // Act — push below 0.0
        _sut.RecordNeedSatisfaction(NeedLevel.OperationalStability, -5.0, "catastrophic failure");

        // Assert
        _sut.GetSatisfaction(NeedLevel.OperationalStability).Should().Be(0.0);
    }

    [Fact]
    public void RecordNeedSatisfaction_RecordsEvent()
    {
        // Act
        _sut.RecordNeedSatisfaction(NeedLevel.Recognition, 0.1, "praise");

        // Assert
        var history = _sut.GetEventHistory();
        history.Should().HaveCount(1);
        history[0].Cause.Should().Be("praise");
    }

    // --- SetNeedSatisfaction ---

    [Fact]
    public void SetNeedSatisfaction_NullCause_ThrowsArgumentNullException()
    {
        var act = () => _sut.SetNeedSatisfaction(NeedLevel.Safety, 0.5, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetNeedSatisfaction_SetsAbsoluteValue()
    {
        // Act
        _sut.SetNeedSatisfaction(NeedLevel.SocialConnection, 0.9, "new connection");

        // Assert
        _sut.GetSatisfaction(NeedLevel.SocialConnection).Should().Be(0.9);
    }

    [Fact]
    public void SetNeedSatisfaction_ClampsValue()
    {
        // Act
        _sut.SetNeedSatisfaction(NeedLevel.SocialConnection, 2.0, "extreme");

        // Assert
        _sut.GetSatisfaction(NeedLevel.SocialConnection).Should().Be(1.0);
    }

    // --- IsNeedBlocking ---

    [Fact]
    public void IsNeedBlocking_SatisfactionAboveThreshold_ReturnsFalse()
    {
        // Default OperationalStability = 0.8
        _sut.IsNeedBlocking(NeedLevel.OperationalStability).Should().BeFalse();
    }

    [Fact]
    public void IsNeedBlocking_SatisfactionBelowThreshold_ReturnsTrue()
    {
        // Arrange
        _sut.SetNeedSatisfaction(NeedLevel.Safety, 0.1, "danger");

        // Act & Assert
        _sut.IsNeedBlocking(NeedLevel.Safety).Should().BeTrue();
    }

    [Fact]
    public void IsNeedBlocking_SatisfactionAtThreshold_ReturnsFalse()
    {
        // Arrange — at exactly 0.3 threshold (not below)
        _sut.SetNeedSatisfaction(NeedLevel.Safety, NeedsHierarchy.BlockingThreshold, "at boundary");

        // Act & Assert
        _sut.IsNeedBlocking(NeedLevel.Safety).Should().BeFalse();
    }

    // --- IsBlockedByLowerNeeds ---

    [Fact]
    public void IsBlockedByLowerNeeds_NoBlockingNeeds_ReturnsFalse()
    {
        // Default state: all needs above threshold
        _sut.IsBlockedByLowerNeeds(NeedLevel.SelfActualization).Should().BeFalse();
    }

    [Fact]
    public void IsBlockedByLowerNeeds_LowerNeedBlocked_ReturnsTrue()
    {
        // Arrange
        _sut.SetNeedSatisfaction(NeedLevel.OperationalStability, 0.1, "failure");

        // Act & Assert
        _sut.IsBlockedByLowerNeeds(NeedLevel.SelfActualization).Should().BeTrue();
        _sut.IsBlockedByLowerNeeds(NeedLevel.Safety).Should().BeTrue();
    }

    [Fact]
    public void IsBlockedByLowerNeeds_LowestLevel_ReturnsFalse()
    {
        // OperationalStability is lowest — nothing can block it
        _sut.IsBlockedByLowerNeeds(NeedLevel.OperationalStability).Should().BeFalse();
    }

    // --- GetAllNeedStates ---

    [Fact]
    public void GetAllNeedStates_ReturnsAllFiveLevels()
    {
        // Act
        var states = _sut.GetAllNeedStates();

        // Assert
        states.Should().HaveCount(5);
    }

    [Fact]
    public void GetAllNeedStates_ExactlyOneIsActive()
    {
        // Act
        var states = _sut.GetAllNeedStates();

        // Assert
        states.Count(s => s.IsActive).Should().Be(1);
    }

    [Fact]
    public void GetAllNeedStates_OrderedFromLowestToHighest()
    {
        // Act
        var states = _sut.GetAllNeedStates();

        // Assert
        states[0].Level.Should().Be(NeedLevel.OperationalStability);
        states[^1].Level.Should().Be(NeedLevel.SelfActualization);
    }

    // --- GetEventHistory ---

    [Fact]
    public void GetEventHistory_NoEvents_ReturnsEmpty()
    {
        _sut.GetEventHistory().Should().BeEmpty();
    }

    [Fact]
    public void GetEventHistory_RespectsCountLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
            _sut.RecordNeedSatisfaction(NeedLevel.Safety, 0.01, $"event-{i}");

        // Act
        var history = _sut.GetEventHistory(3);

        // Assert
        history.Should().HaveCount(3);
    }

    [Fact]
    public void GetEventHistory_ReturnsDescendingOrder()
    {
        // Arrange
        _sut.RecordNeedSatisfaction(NeedLevel.Safety, 0.01, "first");
        _sut.RecordNeedSatisfaction(NeedLevel.Safety, 0.01, "second");

        // Act
        var history = _sut.GetEventHistory();

        // Assert
        history[0].Cause.Should().Be("second");
    }

    // --- BlockingThreshold constant ---

    [Fact]
    public void BlockingThreshold_IsPointThree()
    {
        NeedsHierarchy.BlockingThreshold.Should().Be(0.3);
    }
}

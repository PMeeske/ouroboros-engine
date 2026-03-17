// <copyright file="AgencyModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

/// <summary>
/// Unit tests for the AgencyModel class implementing Wegner's Apparent Mental Causation model.
/// </summary>
[Trait("Category", "Unit")]
public class AgencyModelTests
{
    private readonly AgencyModel _sut;

    public AgencyModelTests()
    {
        _sut = new AgencyModel();
    }

    // --- RecordVoluntaryAction ---

    [Fact]
    public void RecordVoluntaryAction_WithValidInputs_ReturnsSuccess()
    {
        // Act
        var result = _sut.RecordVoluntaryAction("action-1", "expected outcome");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void RecordVoluntaryAction_EmptyActionId_ReturnsFailure()
    {
        // Act
        var result = _sut.RecordVoluntaryAction("", "expected outcome");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Action ID");
    }

    [Fact]
    public void RecordVoluntaryAction_NullActionId_ReturnsFailure()
    {
        // Act
        var result = _sut.RecordVoluntaryAction(null!, "expected outcome");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void RecordVoluntaryAction_EmptyPredictedOutcome_ReturnsFailure()
    {
        // Act
        var result = _sut.RecordVoluntaryAction("action-1", "");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Predicted outcome");
    }

    [Fact]
    public void RecordVoluntaryAction_DuplicateActionId_ReturnsFailure()
    {
        // Arrange
        _sut.RecordVoluntaryAction("action-1", "outcome");

        // Act
        var result = _sut.RecordVoluntaryAction("action-1", "different outcome");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already recorded");
    }

    // --- RecordActionOutcome ---

    [Fact]
    public void RecordActionOutcome_WithMatchingOutcome_HighAgencyScore()
    {
        // Arrange
        _sut.RecordVoluntaryAction("action-1", "task completed successfully");

        // Act
        var result = _sut.RecordActionOutcome("action-1", "task completed successfully");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgencyScore.Should().Be(1.0);
    }

    [Fact]
    public void RecordActionOutcome_WithPartialMatch_IntermediateScore()
    {
        // Arrange
        _sut.RecordVoluntaryAction("action-1", "task completed successfully");

        // Act
        var result = _sut.RecordActionOutcome("action-1", "task failed unexpectedly");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgencyScore.Should().BeGreaterThan(0.0);
        result.Value.AgencyScore.Should().BeLessThan(1.0);
    }

    [Fact]
    public void RecordActionOutcome_WithNoOverlap_ZeroAgencyScore()
    {
        // Arrange
        _sut.RecordVoluntaryAction("action-1", "alpha beta gamma");

        // Act
        var result = _sut.RecordActionOutcome("action-1", "delta epsilon zeta");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgencyScore.Should().Be(0.0);
    }

    [Fact]
    public void RecordActionOutcome_EmptyActionId_ReturnsFailure()
    {
        // Act
        var result = _sut.RecordActionOutcome("", "outcome");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void RecordActionOutcome_EmptyActualOutcome_ReturnsFailure()
    {
        // Arrange
        _sut.RecordVoluntaryAction("action-1", "predicted");

        // Act
        var result = _sut.RecordActionOutcome("action-1", "");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void RecordActionOutcome_NoPrediction_ReturnsFailure()
    {
        // Act
        var result = _sut.RecordActionOutcome("unknown-action", "some outcome");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No prediction found");
    }

    // --- GetAgencyScore ---

    [Fact]
    public void GetAgencyScore_AfterRecordingOutcome_ReturnsScore()
    {
        // Arrange
        _sut.RecordVoluntaryAction("action-1", "outcome A");
        _sut.RecordActionOutcome("action-1", "outcome A");

        // Act
        var result = _sut.GetAgencyScore("action-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1.0);
    }

    [Fact]
    public void GetAgencyScore_NoAttribution_ReturnsFailure()
    {
        // Act
        var result = _sut.GetAgencyScore("nonexistent");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // --- GetOverallAgencyScore ---

    [Fact]
    public void GetOverallAgencyScore_NoActions_ReturnsZero()
    {
        // Act
        var result = _sut.GetOverallAgencyScore();

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void GetOverallAgencyScore_WithActions_ReturnsAverage()
    {
        // Arrange
        _sut.RecordVoluntaryAction("a1", "same words here");
        _sut.RecordActionOutcome("a1", "same words here");
        _sut.RecordVoluntaryAction("a2", "same words here");
        _sut.RecordActionOutcome("a2", "same words here");

        // Act
        var result = _sut.GetOverallAgencyScore();

        // Assert
        result.Should().Be(1.0);
    }

    // --- AttributeAgencyAsync ---

    [Fact]
    public async Task AttributeAgencyAsync_SystemTriggered_ReturnsTriggered()
    {
        // Act
        var result = await _sut.AttributeAgencyAsync("a1", false, true, 100);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AgencyType.Triggered);
    }

    [Fact]
    public async Task AttributeAgencyAsync_UserRequestedFastReaction_ReturnsReflexive()
    {
        // Act
        var result = await _sut.AttributeAgencyAsync("a1", true, false, 30);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AgencyType.Reflexive);
    }

    [Fact]
    public async Task AttributeAgencyAsync_UserRequestedDeliberate_ReturnsReactive()
    {
        // Act
        var result = await _sut.AttributeAgencyAsync("a1", true, false, 200);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AgencyType.Reactive);
    }

    [Fact]
    public async Task AttributeAgencyAsync_Autonomous_ReturnsVoluntary()
    {
        // Act
        var result = await _sut.AttributeAgencyAsync("a1", false, false, 500);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AgencyType.Voluntary);
    }

    [Fact]
    public async Task AttributeAgencyAsync_EmptyActionId_ReturnsFailure()
    {
        // Act
        var result = await _sut.AttributeAgencyAsync("", false, false, 100);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // --- GetAllAttributions ---

    [Fact]
    public void GetAllAttributions_NoAttributions_ReturnsEmpty()
    {
        // Act
        var result = _sut.GetAllAttributions();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllAttributions_WithAttributions_ReturnsMostRecentFirst()
    {
        // Arrange
        _sut.RecordVoluntaryAction("a1", "outcome one");
        _sut.RecordActionOutcome("a1", "outcome one");
        _sut.RecordVoluntaryAction("a2", "outcome two");
        _sut.RecordActionOutcome("a2", "outcome two");

        // Act
        var result = _sut.GetAllAttributions();

        // Assert
        result.Should().HaveCount(2);
        result[0].ActionId.Should().Be("a2");
        result[1].ActionId.Should().Be("a1");
    }

    // --- Pruning ---

    [Fact]
    public void RecordVoluntaryAction_ExceedsMaxActions_PrunesOldest()
    {
        // Arrange: Record 201 actions to trigger pruning (max is 200)
        for (int i = 0; i < 201; i++)
        {
            _sut.RecordVoluntaryAction($"action-{i}", $"outcome {i}");
        }

        // Act: The first action should have been pruned
        var result = _sut.RecordVoluntaryAction("action-0", "new outcome");

        // Assert: action-0 was pruned, so recording it again should succeed
        result.IsSuccess.Should().BeTrue();
    }
}

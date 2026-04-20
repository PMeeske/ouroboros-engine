// <copyright file="FlowStateEngineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.Affect;

namespace Ouroboros.Agent.Tests.MetaAI.Affect;

/// <summary>
/// Unit tests for <see cref="FlowStateEngine"/>.
/// </summary>
[Trait("Category", "Unit")]
public class FlowStateEngineTests
{
    private readonly FlowStateEngine _sut = new();

    // --- AssessFlowState ---

    [Fact]
    public void AssessFlowState_HighSkillHighChallenge_ReturnsFlowState()
    {
        // Arrange & Act
        var result = _sut.AssessFlowState(0.8, 0.8, 0.9);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be(FlowState.Flow);
    }

    [Fact]
    public void AssessFlowState_LowSkillLowChallenge_ReturnsApathy()
    {
        // Arrange & Act
        var result = _sut.AssessFlowState(0.1, 0.1, 0.2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be(FlowState.Apathy);
    }

    [Fact]
    public void AssessFlowState_LowSkillHighChallenge_ReturnsAnxiety()
    {
        // Arrange & Act
        var result = _sut.AssessFlowState(0.1, 0.8, 0.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be(FlowState.Anxiety);
    }

    [Fact]
    public void AssessFlowState_HighSkillLowChallenge_ReturnsRelaxation()
    {
        // Arrange & Act
        var result = _sut.AssessFlowState(0.8, 0.1, 0.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be(FlowState.Relaxation);
    }

    [Fact]
    public void AssessFlowState_ModerateSkillHighChallenge_ReturnsArousal()
    {
        // Arrange & Act
        var result = _sut.AssessFlowState(0.5, 0.8, 0.6);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be(FlowState.Arousal);
    }

    [Fact]
    public void AssessFlowState_HighSkillModerateChallenge_ReturnsControl()
    {
        // Arrange & Act
        var result = _sut.AssessFlowState(0.8, 0.5, 0.6);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be(FlowState.Control);
    }

    [Fact]
    public void AssessFlowState_ClampsInputValues()
    {
        // Arrange & Act — values above 1.0 and below 0.0
        var result = _sut.AssessFlowState(2.0, -0.5, 1.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SkillLevel.Should().Be(1.0);
        result.Value.ChallengeLevel.Should().Be(0.0);
        result.Value.Absorption.Should().Be(1.0);
    }

    [Fact]
    public void AssessFlowState_FlowState_HasHighIntrinsicReward()
    {
        // Arrange & Act
        var result = _sut.AssessFlowState(0.8, 0.8, 0.9);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IntrinsicReward.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void AssessFlowState_NonFlowState_HasLowIntrinsicReward()
    {
        // Arrange & Act
        var result = _sut.AssessFlowState(0.1, 0.1, 0.3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IntrinsicReward.Should().BeLessThan(0.5);
    }

    [Fact]
    public void AssessFlowState_FlowState_HasCompressedTimeDistortion()
    {
        // Arrange & Act
        var result = _sut.AssessFlowState(0.8, 0.8, 0.9);

        // Assert — flow time distortion base is 0.5, further reduced by absorption
        result.IsSuccess.Should().BeTrue();
        result.Value.TimeDistortion.Should().BeLessThan(0.5);
    }

    [Fact]
    public void AssessFlowState_Boredom_HasExpandedTimeDistortion()
    {
        // Arrange — moderate skill, low challenge = boredom
        var result = _sut.AssessFlowState(0.5, 0.1, 0.2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be(FlowState.Boredom);
        result.Value.TimeDistortion.Should().BeGreaterThan(1.5);
    }

    [Fact]
    public void AssessFlowState_RecordsToHistory()
    {
        // Arrange & Act
        _sut.AssessFlowState(0.5, 0.5, 0.5);
        _sut.AssessFlowState(0.8, 0.8, 0.8);

        // Assert
        var history = _sut.GetAssessmentHistory();
        history.Should().HaveCount(2);
    }

    // --- RecordTaskEngagement ---

    [Fact]
    public void RecordTaskEngagement_NullTaskId_ThrowsArgumentNullException()
    {
        var act = () => _sut.RecordTaskEngagement(null!, 0.5, 0.5, TimeSpan.FromMinutes(10));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordTaskEngagement_ValidInput_RecordsEngagement()
    {
        // Act
        _sut.RecordTaskEngagement("task-1", 0.7, 0.8, TimeSpan.FromMinutes(30));

        // Assert — no exception means success
        _sut.GetAverageFlowDuration().Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void RecordTaskEngagement_ClampsValues()
    {
        // Act — should not throw even with out-of-range values
        var act = () => _sut.RecordTaskEngagement("task-1", 2.0, -0.5, TimeSpan.FromMinutes(10));
        act.Should().NotThrow();
    }

    // --- OptimizeForFlowAsync ---

    [Fact]
    public async Task OptimizeForFlowAsync_NullDescription_ThrowsArgumentNullException()
    {
        var act = () => _sut.OptimizeForFlowAsync(null!, 0.5);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OptimizeForFlowAsync_CancelledToken_ReturnsFailure()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _sut.OptimizeForFlowAsync("task", 0.5, cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task OptimizeForFlowAsync_LowSkill_RecommendsChallengeReduction()
    {
        // Act
        var result = await _sut.OptimizeForFlowAsync("complex task", 0.3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PredictedFlowProbability.Should().BeLessThan(0.5);
        result.Value.SuggestedAdjustment.Should().Contain("below flow threshold");
    }

    [Fact]
    public async Task OptimizeForFlowAsync_HighSkill_RecommendsMatchedChallenge()
    {
        // Act
        var result = await _sut.OptimizeForFlowAsync("complex task", 0.8);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PredictedFlowProbability.Should().BeGreaterThan(0.5);
        result.Value.SuggestedAdjustment.Should().Contain("skill level");
    }

    // --- GetFlowEntryRate ---

    [Fact]
    public void GetFlowEntryRate_NoAssessments_ReturnsZero()
    {
        _sut.GetFlowEntryRate().Should().Be(0.0);
    }

    [Fact]
    public void GetFlowEntryRate_WithFlowAndNonFlow_ReturnsCorrectRate()
    {
        // Arrange — 1 flow + 1 non-flow = 50% rate
        _sut.AssessFlowState(0.8, 0.8, 0.9); // flow
        _sut.AssessFlowState(0.1, 0.1, 0.1); // apathy

        // Act
        var rate = _sut.GetFlowEntryRate();

        // Assert
        rate.Should().Be(0.5);
    }

    // --- GetAverageFlowDuration ---

    [Fact]
    public void GetAverageFlowDuration_NoFlowEpisodes_ReturnsZero()
    {
        _sut.GetAverageFlowDuration().Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetAverageFlowDuration_WithFlowEpisodes_ReturnsAverage()
    {
        // Arrange — record flow episodes (high skill + high challenge)
        _sut.RecordTaskEngagement("task-1", 0.8, 0.8, TimeSpan.FromMinutes(30));
        _sut.RecordTaskEngagement("task-2", 0.9, 0.9, TimeSpan.FromMinutes(60));

        // Act
        var avgDuration = _sut.GetAverageFlowDuration();

        // Assert
        avgDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    // --- GetAssessmentHistory ---

    [Fact]
    public void GetAssessmentHistory_RespectsCountLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
            _sut.AssessFlowState(0.5, 0.5, 0.5);

        // Act
        var history = _sut.GetAssessmentHistory(3);

        // Assert
        history.Should().HaveCount(3);
    }

    [Fact]
    public void GetAssessmentHistory_ReturnsDescendingOrder()
    {
        // Arrange
        _sut.AssessFlowState(0.1, 0.1, 0.1);
        _sut.AssessFlowState(0.8, 0.8, 0.8);

        // Act
        var history = _sut.GetAssessmentHistory();

        // Assert — most recent first
        history.Should().HaveCount(2);
        history[0].State.Should().Be(FlowState.Flow);
    }
}

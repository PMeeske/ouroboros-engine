// <copyright file="DevelopmentalModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

/// <summary>
/// Unit tests for the DevelopmentalModel class implementing Piaget + Dreyfus skill model.
/// </summary>
[Trait("Category", "Unit")]
public class DevelopmentalModelTests
{
    private readonly DevelopmentalModel _sut;

    public DevelopmentalModelTests()
    {
        _sut = new DevelopmentalModel();
    }

    // --- GetCurrentStage ---

    [Fact]
    public void GetCurrentStage_UnknownDomain_ReturnsNascent()
    {
        // Act
        var result = _sut.GetCurrentStage("unknown-domain");

        // Assert
        result.Should().Be(DevelopmentalStage.Nascent);
    }

    [Fact]
    public void GetCurrentStage_NullDomain_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.GetCurrentStage(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0.1, DevelopmentalStage.Nascent)]
    [InlineData(0.3, DevelopmentalStage.Developing)]
    [InlineData(0.5, DevelopmentalStage.Competent)]
    [InlineData(0.7, DevelopmentalStage.Proficient)]
    [InlineData(0.9, DevelopmentalStage.Expert)]
    public void GetCurrentStage_AfterRecording_ReturnsCorrectStage(
        double score, DevelopmentalStage expectedStage)
    {
        // Arrange - record enough to set EMA close to score
        // First recording sets EMA directly
        _sut.RecordSkillProgress("domain", score);

        // Act
        var result = _sut.GetCurrentStage("domain");

        // Assert
        result.Should().Be(expectedStage);
    }

    // --- RecordSkillProgress ---

    [Fact]
    public void RecordSkillProgress_ValidInput_ReturnsSuccess()
    {
        // Act
        var result = _sut.RecordSkillProgress("coding", 0.7);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0.7); // First value sets EMA directly
    }

    [Fact]
    public void RecordSkillProgress_EmptyDomain_ReturnsFailure()
    {
        // Act
        var result = _sut.RecordSkillProgress("", 0.5);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Domain");
    }

    [Fact]
    public void RecordSkillProgress_NullDomain_ReturnsFailure()
    {
        // Act
        var result = _sut.RecordSkillProgress(null!, 0.5);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void RecordSkillProgress_ScoreClamped_TooHigh()
    {
        // Act
        var result = _sut.RecordSkillProgress("domain", 1.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void RecordSkillProgress_ScoreClamped_TooLow()
    {
        // Act
        var result = _sut.RecordSkillProgress("domain", -0.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void RecordSkillProgress_MultipleRecordings_UsesEMA()
    {
        // Arrange
        _sut.RecordSkillProgress("domain", 0.5);

        // Act
        var result = _sut.RecordSkillProgress("domain", 1.0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // EMA should be between the two values
        result.Value.Should().BeGreaterThan(0.5);
        result.Value.Should().BeLessThan(1.0);
    }

    // --- CheckMilestoneAsync ---

    [Fact]
    public async Task CheckMilestoneAsync_PerformanceAboveThreshold_ReturnsSuccess()
    {
        // Arrange
        _sut.RecordSkillProgress("coding", 0.8);

        // Act
        var result = await _sut.CheckMilestoneAsync("coding", "intermediate", 0.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Domain.Should().Be("coding");
        result.Value.Description.Should().Be("intermediate");
    }

    [Fact]
    public async Task CheckMilestoneAsync_PerformanceBelowThreshold_ReturnsFailure()
    {
        // Arrange
        _sut.RecordSkillProgress("coding", 0.3);

        // Act
        var result = await _sut.CheckMilestoneAsync("coding", "advanced", 0.9);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("threshold");
    }

    [Fact]
    public async Task CheckMilestoneAsync_AlreadyAchieved_ReturnsFailure()
    {
        // Arrange
        _sut.RecordSkillProgress("coding", 0.8);
        await _sut.CheckMilestoneAsync("coding", "intermediate", 0.5);

        // Act
        var result = await _sut.CheckMilestoneAsync("coding", "intermediate", 0.5);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already achieved");
    }

    [Fact]
    public async Task CheckMilestoneAsync_UnknownDomain_ReturnsFailure()
    {
        // Act
        var result = await _sut.CheckMilestoneAsync("unknown", "milestone", 0.5);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // --- GetAchievedMilestones ---

    [Fact]
    public void GetAchievedMilestones_NoMilestones_ReturnsEmpty()
    {
        // Act
        var result = _sut.GetAchievedMilestones();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAchievedMilestones_WithMilestones_ReturnsOrderedByTime()
    {
        // Arrange
        _sut.RecordSkillProgress("coding", 0.8);
        _sut.RecordSkillProgress("design", 0.7);
        await _sut.CheckMilestoneAsync("coding", "milestone-1", 0.5);
        await _sut.CheckMilestoneAsync("design", "milestone-2", 0.5);

        // Act
        var result = _sut.GetAchievedMilestones();

        // Assert
        result.Should().HaveCount(2);
    }

    // --- GetAllStages ---

    [Fact]
    public void GetAllStages_NoDomains_ReturnsEmpty()
    {
        // Act
        var result = _sut.GetAllStages();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllStages_WithDomains_ReturnsAllStages()
    {
        // Arrange
        _sut.RecordSkillProgress("coding", 0.5);
        _sut.RecordSkillProgress("design", 0.1);

        // Act
        var result = _sut.GetAllStages();

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKey("coding");
        result.Should().ContainKey("design");
    }
}

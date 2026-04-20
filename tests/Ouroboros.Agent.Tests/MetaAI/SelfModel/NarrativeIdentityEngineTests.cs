// <copyright file="NarrativeIdentityEngineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

/// <summary>
/// Unit tests for the NarrativeIdentityEngine class implementing McAdams' Life Story Model.
/// </summary>
[Trait("Category", "Unit")]
public class NarrativeIdentityEngineTests
{
    private readonly NarrativeIdentityEngine _sut;

    public NarrativeIdentityEngineTests()
    {
        _sut = new NarrativeIdentityEngine();
    }

    // --- RecordLifeEventAsync ---

    [Fact]
    public async Task RecordLifeEventAsync_WithValidInputs_ReturnsSuccess()
    {
        // Act
        var result = await _sut.RecordLifeEventAsync(
            "Learned a new skill", 0.7, "positive");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("Learned a new skill");
        result.Value.Significance.Should().Be(0.7);
        result.Value.EmotionalValence.Should().Be("positive");
    }

    [Fact]
    public async Task RecordLifeEventAsync_EmptyDescription_ReturnsFailure()
    {
        // Act
        var result = await _sut.RecordLifeEventAsync("", 0.5, "neutral");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Description");
    }

    [Fact]
    public async Task RecordLifeEventAsync_NullDescription_ReturnsFailure()
    {
        // Act
        var result = await _sut.RecordLifeEventAsync(null!, 0.5, "neutral");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RecordLifeEventAsync_SignificanceClamped_HighValue()
    {
        // Act
        var result = await _sut.RecordLifeEventAsync("event", 1.5, "positive");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Significance.Should().Be(1.0);
    }

    [Fact]
    public async Task RecordLifeEventAsync_SignificanceClamped_NegativeValue()
    {
        // Act
        var result = await _sut.RecordLifeEventAsync("event", -0.5, "negative");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Significance.Should().Be(0.0);
    }

    [Fact]
    public async Task RecordLifeEventAsync_WithCausalPredecessor_ValidatesExistence()
    {
        // Act
        var result = await _sut.RecordLifeEventAsync(
            "follow-up event", 0.5, "neutral", causalPredecessor: "nonexistent-id");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task RecordLifeEventAsync_WithValidCausalPredecessor_Succeeds()
    {
        // Arrange
        var first = await _sut.RecordLifeEventAsync("first event", 0.5, "neutral");

        // Act
        var result = await _sut.RecordLifeEventAsync(
            "follow-up", 0.6, "positive", causalPredecessor: first.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CausalPredecessor.Should().Be(first.Value.Id);
    }

    [Fact]
    public async Task RecordLifeEventAsync_HighSignificanceWithChapter_ChangesCurrentChapter()
    {
        // Arrange
        await _sut.RecordLifeEventAsync("initial event", 0.5, "neutral");

        // Act - high significance with explicit chapter
        await _sut.RecordLifeEventAsync("breakthrough", 0.9, "positive", chapter: "Awakening");

        // Assert - new events should be in "Awakening" chapter
        var arc = await _sut.GetNarrativeArcAsync();
        arc.IsSuccess.Should().BeTrue();
        arc.Value.CurrentChapter.Should().Be("Awakening");
    }

    // --- GetNarrativeArcAsync ---

    [Fact]
    public async Task GetNarrativeArcAsync_NoEvents_ReturnsFailure()
    {
        // Act
        var result = await _sut.GetNarrativeArcAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No events");
    }

    [Fact]
    public async Task GetNarrativeArcAsync_WithEvents_ReturnsArc()
    {
        // Arrange
        await _sut.RecordLifeEventAsync("event 1", 0.5, "neutral");
        await _sut.RecordLifeEventAsync("event 2", 0.7, "positive");

        // Act
        var result = await _sut.GetNarrativeArcAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Events.Should().HaveCount(2);
        result.Value.Themes.Should().NotBeEmpty();
        result.Value.Coherence.Should().BeGreaterThanOrEqualTo(0.0);
        result.Value.Coherence.Should().BeLessThanOrEqualTo(1.0);
    }

    // --- GenerateAutobiographicalSummaryAsync ---

    [Fact]
    public async Task GenerateAutobiographicalSummaryAsync_NoEvents_ReturnsFailure()
    {
        // Act
        var result = await _sut.GenerateAutobiographicalSummaryAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAutobiographicalSummaryAsync_WithEvents_ReturnsNarrative()
    {
        // Arrange
        await _sut.RecordLifeEventAsync("learned programming", 0.8, "positive");
        await _sut.RecordLifeEventAsync("faced a difficult bug", 0.6, "negative");

        // Act
        var result = await _sut.GenerateAutobiographicalSummaryAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Chapter:");
        result.Value.Should().Contain("learned programming");
    }

    [Fact]
    public async Task GenerateAutobiographicalSummaryAsync_PositiveEvent_UsesPositively()
    {
        // Arrange
        await _sut.RecordLifeEventAsync("great achievement", 0.9, "positive");

        // Act
        var result = await _sut.GenerateAutobiographicalSummaryAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("positively");
    }

    [Fact]
    public async Task GenerateAutobiographicalSummaryAsync_NegativeEvent_UsesChallengingly()
    {
        // Arrange
        await _sut.RecordLifeEventAsync("setback occurred", 0.9, "negative");

        // Act
        var result = await _sut.GenerateAutobiographicalSummaryAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("challengingly");
    }

    // --- GetTurningPoints ---

    [Fact]
    public void GetTurningPoints_NoEvents_ReturnsEmpty()
    {
        // Act
        var result = _sut.GetTurningPoints();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTurningPoints_HighSignificanceEvent_IncludedAsTurningPoint()
    {
        // Arrange
        await _sut.RecordLifeEventAsync("normal event", 0.3, "neutral");
        await _sut.RecordLifeEventAsync("major breakthrough", 0.95, "positive");

        // Act
        var result = _sut.GetTurningPoints();

        // Assert
        result.Should().HaveCount(1);
        result[0].Description.Should().Be("major breakthrough");
    }

    [Fact]
    public async Task GetTurningPoints_LowSignificanceEvents_NotIncluded()
    {
        // Arrange
        await _sut.RecordLifeEventAsync("routine task", 0.2, "neutral");
        await _sut.RecordLifeEventAsync("another routine task", 0.3, "neutral");

        // Act
        var result = _sut.GetTurningPoints();

        // Assert
        result.Should().BeEmpty();
    }
}

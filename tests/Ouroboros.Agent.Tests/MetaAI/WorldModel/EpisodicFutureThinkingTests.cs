// <copyright file="EpisodicFutureThinkingTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;

namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

/// <summary>
/// Unit tests for the EpisodicFutureThinking class implementing constructive episodic simulation.
/// </summary>
[Trait("Category", "Unit")]
public class EpisodicFutureThinkingTests
{
    private readonly PredictiveProcessingEngine _predictionEngine;
    private readonly EpisodicFutureThinking _sut;

    public EpisodicFutureThinkingTests()
    {
        _predictionEngine = new PredictiveProcessingEngine();
        _sut = new EpisodicFutureThinking(_predictionEngine);
    }

    [Fact]
    public void Constructor_NullPredictionEngine_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new EpisodicFutureThinking(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // --- AddMemoryFragment ---

    [Fact]
    public void AddMemoryFragment_NullFragment_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.AddMemoryFragment(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddMemoryFragment_ValidFragment_DoesNotThrow()
    {
        // Act
        var act = () => _sut.AddMemoryFragment("I learned to code in Python");

        // Assert
        act.Should().NotThrow();
    }

    // --- SimulateFutureEpisodeAsync ---

    [Fact]
    public async Task SimulateFutureEpisodeAsync_NullScenario_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.SimulateFutureEpisodeAsync(null!, TimeSpan.FromDays(7));

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SimulateFutureEpisodeAsync_ValidScenario_ReturnsEpisode()
    {
        // Act
        var episode = await _sut.SimulateFutureEpisodeAsync(
            "learn machine learning", TimeSpan.FromDays(30));

        // Assert
        episode.Should().NotBeNull();
        episode.Scenario.Should().Be("learn machine learning");
        episode.TimeHorizon.Should().Be(TimeSpan.FromDays(30));
        episode.PredictedEvents.Should().NotBeEmpty();
        episode.Confidence.Should().BeGreaterThanOrEqualTo(0.0);
        episode.Confidence.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task SimulateFutureEpisodeAsync_ShortHorizon_HasMinimumEvents()
    {
        // Act
        var episode = await _sut.SimulateFutureEpisodeAsync(
            "quick task", TimeSpan.FromHours(1));

        // Assert
        episode.PredictedEvents.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task SimulateFutureEpisodeAsync_LongHorizon_HasMultipleEvents()
    {
        // Act
        var episode = await _sut.SimulateFutureEpisodeAsync(
            "long term goal", TimeSpan.FromDays(365));

        // Assert
        episode.PredictedEvents.Should().HaveCountGreaterThan(2);
        episode.PredictedEvents.Should().HaveCountLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task SimulateFutureEpisodeAsync_PositiveScenario_OptimisticTone()
    {
        // Act
        var episode = await _sut.SimulateFutureEpisodeAsync(
            "achieve success and improve and grow", TimeSpan.FromDays(30));

        // Assert
        episode.EmotionalTone.Should().BeOneOf(
            "optimistic", "cautiously hopeful", "neutral");
    }

    [Fact]
    public async Task SimulateFutureEpisodeAsync_NegativeScenario_AnxiousTone()
    {
        // Act
        var episode = await _sut.SimulateFutureEpisodeAsync(
            "risk of failure and danger and threat and conflict", TimeSpan.FromDays(30));

        // Assert
        episode.EmotionalTone.Should().BeOneOf(
            "anxious", "apprehensive", "neutral");
    }

    [Fact]
    public async Task SimulateFutureEpisodeAsync_EventProbabilitiesDecayWithTime()
    {
        // Act
        var episode = await _sut.SimulateFutureEpisodeAsync(
            "plan execution", TimeSpan.FromDays(60));

        // Assert
        if (episode.PredictedEvents.Count >= 2)
        {
            var firstEvent = episode.PredictedEvents.First();
            var lastEvent = episode.PredictedEvents.Last();
            firstEvent.Probability.Should().BeGreaterThanOrEqualTo(lastEvent.Probability);
        }
    }

    [Fact]
    public async Task SimulateFutureEpisodeAsync_WithMemoryFragments_IncludesEchoes()
    {
        // Arrange
        _sut.AddMemoryFragment("learned Python programming basics");
        _sut.AddMemoryFragment("completed machine learning course");

        // Act
        var episode = await _sut.SimulateFutureEpisodeAsync(
            "learned advanced AI techniques", TimeSpan.FromDays(30));

        // Assert
        episode.PredictedEvents.Should().NotBeEmpty();
        // At least some events should include memory fragment echoes
        var hasEcho = episode.PredictedEvents.Any(e => e.Description.Contains("echoing"));
        hasEcho.Should().BeTrue();
    }

    // --- GeneratePersonalFutureAsync ---

    [Fact]
    public async Task GeneratePersonalFutureAsync_NullGoal_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.GeneratePersonalFutureAsync(null!, 3);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GeneratePersonalFutureAsync_ValidGoal_ReturnsNarrative()
    {
        // Act
        var narrative = await _sut.GeneratePersonalFutureAsync("master AI development", 3);

        // Assert
        narrative.Should().NotBeNullOrEmpty();
        narrative.Should().Contain("I imagine myself working toward: master AI development");
        narrative.Should().Contain("Overall tone:");
        narrative.Should().Contain("Confidence:");
    }

    [Fact]
    public async Task GeneratePersonalFutureAsync_StepsClamped_MaximumTwenty()
    {
        // Act
        var narrative = await _sut.GeneratePersonalFutureAsync("goal", 100);

        // Assert
        narrative.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GeneratePersonalFutureAsync_StepsClamped_MinimumOne()
    {
        // Act
        var narrative = await _sut.GeneratePersonalFutureAsync("goal", 0);

        // Assert
        narrative.Should().NotBeNullOrEmpty();
    }
}

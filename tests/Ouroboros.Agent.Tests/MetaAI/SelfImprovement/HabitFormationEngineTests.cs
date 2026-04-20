// <copyright file="HabitFormationEngineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfImprovement;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class HabitFormationEngineTests
{
    // ── RecordActionPattern ─────────────────────────────────────────

    [Fact]
    public void RecordActionPattern_NullCue_Throws()
    {
        var engine = new HabitFormationEngine();
        var act = () => engine.RecordActionPattern(null!, "routine", "reward", 0.8);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordActionPattern_NullRoutine_Throws()
    {
        var engine = new HabitFormationEngine();
        var act = () => engine.RecordActionPattern("cue", null!, "reward", 0.8);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordActionPattern_NullReward_Throws()
    {
        var engine = new HabitFormationEngine();
        var act = () => engine.RecordActionPattern("cue", "routine", null!, 0.8);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordActionPattern_FirstOccurrence_CreatesHabitWithRepetitionOne()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Act
        var habit = engine.RecordActionPattern("morning alarm", "check email", "inbox clear", 0.9);

        // Assert
        habit.Cue.Should().Be("morning alarm");
        habit.Routine.Should().Be("check email");
        habit.Reward.Should().Be("inbox clear");
        habit.RepetitionCount.Should().Be(1);
        habit.AverageQuality.Should().Be(0.9);
    }

    [Fact]
    public void RecordActionPattern_SameCueAndRoutine_IncrementsRepetitionCount()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Act
        engine.RecordActionPattern("cue", "routine", "reward", 0.8);
        var habit = engine.RecordActionPattern("cue", "routine", "reward", 0.6);

        // Assert
        habit.RepetitionCount.Should().Be(2);
    }

    [Fact]
    public void RecordActionPattern_RepeatedExecutions_IncreasesAutomaticity()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Act
        Habit habit = null!;
        for (int i = 0; i < 20; i++)
        {
            habit = engine.RecordActionPattern("cue", "routine", "reward", 0.8);
        }

        // Assert — automaticity = 1 - exp(-0.05 * 20) ≈ 0.6321
        habit.AutomaticityScore.Should().BeGreaterThan(0.5);
        habit.RepetitionCount.Should().Be(20);
    }

    [Fact]
    public void RecordActionPattern_UpdatesAverageQuality()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Act
        engine.RecordActionPattern("cue", "routine", "reward", 1.0);
        var habit = engine.RecordActionPattern("cue", "routine", "reward", 0.5);

        // Assert — average of 1.0 and 0.5 = 0.75
        habit.AverageQuality.Should().BeApproximately(0.75, 0.001);
    }

    [Fact]
    public void RecordActionPattern_QualityClamped_AboveOne()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Act — quality > 1 should be clamped to 1.0
        var habit = engine.RecordActionPattern("cue", "routine", "reward", 2.0);

        // Assert
        habit.AverageQuality.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void RecordActionPattern_QualityClamped_BelowZero()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Act — quality < 0 should be clamped to 0.0
        var habit = engine.RecordActionPattern("cue", "routine", "reward", -0.5);

        // Assert
        habit.AverageQuality.Should().BeGreaterThanOrEqualTo(0.0);
    }

    // ── IsAutomatic ─────────────────────────────────────────────────

    [Fact]
    public void IsAutomatic_NullRoutinePattern_Throws()
    {
        var engine = new HabitFormationEngine();
        var act = () => engine.IsAutomatic(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsAutomatic_NewHabit_ReturnsFalse()
    {
        // Arrange
        var engine = new HabitFormationEngine();
        engine.RecordActionPattern("cue", "my routine", "reward", 0.8);

        // Act
        var result = engine.IsAutomatic("my routine");

        // Assert — 1 repetition is far from automatic
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAutomatic_HighRepetitionHabit_ReturnsTrue()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Act — 100 repetitions: automaticity = 1 - exp(-5) ≈ 0.993
        for (int i = 0; i < 100; i++)
        {
            engine.RecordActionPattern("cue", "habitual routine", "reward", 0.9);
        }

        // Assert
        engine.IsAutomatic("habitual routine").Should().BeTrue();
    }

    [Fact]
    public void IsAutomatic_UnknownRoutine_ReturnsFalse()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Act
        var result = engine.IsAutomatic("unknown routine");

        // Assert
        result.Should().BeFalse();
    }

    // ── SuggestHabitAsync ───────────────────────────────────────────

    [Fact]
    public async Task SuggestHabitAsync_NullContext_Throws()
    {
        var engine = new HabitFormationEngine();
        var act = () => engine.SuggestHabitAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SuggestHabitAsync_NoHabits_ReturnsNull()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Act
        var suggestion = await engine.SuggestHabitAsync("some context");

        // Assert
        suggestion.Should().BeNull();
    }

    [Fact]
    public async Task SuggestHabitAsync_MatchingContext_ReturnsSuggestion()
    {
        // Arrange
        var engine = new HabitFormationEngine();
        engine.RecordActionPattern("morning alarm rings", "check email", "inbox clear", 0.9);

        // Act — context contains the cue
        var suggestion = await engine.SuggestHabitAsync("The morning alarm rings loudly");

        // Assert
        suggestion.Should().NotBeNull();
        suggestion!.SuggestedRoutine.Should().Be("check email");
        suggestion.TriggerCue.Should().Be("morning alarm rings");
    }

    [Fact]
    public async Task SuggestHabitAsync_NoMatchingContext_ReturnsNull()
    {
        // Arrange
        var engine = new HabitFormationEngine();
        engine.RecordActionPattern("morning alarm", "check email", "inbox clear", 0.9);

        // Act — context has no overlap with cue
        var suggestion = await engine.SuggestHabitAsync("xyz completely unrelated");

        // Assert
        suggestion.Should().BeNull();
    }

    [Fact]
    public async Task SuggestHabitAsync_CancelledToken_Throws()
    {
        var engine = new HabitFormationEngine();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => engine.SuggestHabitAsync("context", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── GetFormedHabits ─────────────────────────────────────────────

    [Fact]
    public void GetFormedHabits_EmptyEngine_ReturnsEmptyList()
    {
        var engine = new HabitFormationEngine();
        engine.GetFormedHabits().Should().BeEmpty();
    }

    [Fact]
    public void GetFormedHabits_SortedByAutomaticityDescending()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Create a habit with higher repetitions (higher automaticity)
        for (int i = 0; i < 50; i++)
        {
            engine.RecordActionPattern("cue1", "routine1", "reward1", 0.9);
        }

        // Create a habit with fewer repetitions (lower automaticity)
        engine.RecordActionPattern("cue2", "routine2", "reward2", 0.9);

        // Act
        var habits = engine.GetFormedHabits();

        // Assert
        habits.Should().HaveCount(2);
        habits[0].AutomaticityScore.Should().BeGreaterThan(habits[1].AutomaticityScore);
    }

    // ── GetAutomaticHabits ──────────────────────────────────────────

    [Fact]
    public void GetAutomaticHabits_NoAutomaticHabits_ReturnsEmpty()
    {
        // Arrange
        var engine = new HabitFormationEngine();
        engine.RecordActionPattern("cue", "routine", "reward", 0.8);

        // Act
        var automatic = engine.GetAutomaticHabits();

        // Assert
        automatic.Should().BeEmpty();
    }

    [Fact]
    public void GetAutomaticHabits_OnlyReturnsHabitsAboveThreshold()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Create one automatic habit (100 reps -> automaticity ~0.993)
        for (int i = 0; i < 100; i++)
        {
            engine.RecordActionPattern("cue1", "routine1", "reward1", 0.9);
        }

        // Create one non-automatic habit (1 rep)
        engine.RecordActionPattern("cue2", "routine2", "reward2", 0.9);

        // Act
        var automatic = engine.GetAutomaticHabits();

        // Assert
        automatic.Should().HaveCount(1);
        automatic[0].Routine.Should().Be("routine1");
    }

    // ── TotalHabits ─────────────────────────────────────────────────

    [Fact]
    public void TotalHabits_TracksDistinctCueRoutinePairs()
    {
        // Arrange
        var engine = new HabitFormationEngine();

        // Act
        engine.RecordActionPattern("cue1", "routine1", "reward", 0.8);
        engine.RecordActionPattern("cue2", "routine2", "reward", 0.8);
        engine.RecordActionPattern("cue1", "routine1", "reward", 0.9); // update, not new

        // Assert
        engine.TotalHabits.Should().Be(2);
    }
}

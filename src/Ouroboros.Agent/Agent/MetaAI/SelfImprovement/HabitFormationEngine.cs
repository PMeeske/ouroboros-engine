// ==========================================================
// Habit Formation Engine
// Duhigg Habit Loop — cue, routine, reward with
// automaticity tracking
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI.SelfImprovement;

/// <summary>
/// Implements Duhigg's Habit Loop model with automaticity tracking.
/// Automaticity follows the asymptotic formula 1 - exp(-0.05 * repetitions),
/// approaching 1.0 as repetitions increase. Supports a maximum of 200 habits,
/// pruning lowest-quality habits when the limit is reached.
/// </summary>
public sealed class HabitFormationEngine
{
    private const int MaxHabits = 200;
    private const double AutomaticityThreshold = 0.85;
    private const double AutomaticityRate = 0.05;
    private readonly ConcurrentDictionary<string, Habit> _habits = new();

    /// <summary>
    /// Records an action pattern as a habit loop. If a habit with the same
    /// cue and routine exists, it is updated; otherwise a new habit is created.
    /// </summary>
    /// <param name="cue">The trigger context.</param>
    /// <param name="routine">The behavioral pattern.</param>
    /// <param name="reward">The reinforcement received.</param>
    /// <param name="quality">Quality of this execution (0–1).</param>
    /// <returns>The created or updated habit.</returns>
    public Habit RecordActionPattern(string cue, string routine, string reward, double quality)
    {
        ArgumentNullException.ThrowIfNull(cue);
        ArgumentNullException.ThrowIfNull(routine);
        ArgumentNullException.ThrowIfNull(reward);
        quality = Math.Clamp(quality, 0.0, 1.0);

        string key = BuildKey(cue, routine);

        var habit = _habits.AddOrUpdate(
            key,
            _ =>
            {
                EnforceCapacity();
                return new Habit(
                    Id: key,
                    Cue: cue,
                    Routine: routine,
                    Reward: reward,
                    RepetitionCount: 1,
                    AutomaticityScore: CalculateAutomaticity(1),
                    AverageQuality: quality,
                    FirstPerformed: DateTime.UtcNow,
                    LastPerformed: DateTime.UtcNow);
            },
            (_, existing) =>
            {
                int newCount = existing.RepetitionCount + 1;
                double newAvgQuality = (existing.AverageQuality * existing.RepetitionCount + quality) / newCount;
                return existing with
                {
                    RepetitionCount = newCount,
                    AutomaticityScore = CalculateAutomaticity(newCount),
                    AverageQuality = Math.Round(newAvgQuality, 4),
                    Reward = reward,
                    LastPerformed = DateTime.UtcNow
                };
            });

        return habit;
    }

    /// <summary>
    /// Suggests a habit to execute based on the current context matching known cues.
    /// </summary>
    /// <param name="context">Current context to match against known cues.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="HabitSuggestion"/> if a matching habit is found, or null.</returns>
    public Task<HabitSuggestion?> SuggestHabitAsync(string context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        HabitSuggestion? bestSuggestion = null;
        double bestMatch = 0.0;

        foreach (var habit in _habits.Values)
        {
            double matchStrength = CalculateCueMatch(context, habit.Cue);
            if (matchStrength > bestMatch && matchStrength > 0.3)
            {
                bestMatch = matchStrength;
                bestSuggestion = new HabitSuggestion(
                    SuggestedRoutine: habit.Routine,
                    TriggerCue: habit.Cue,
                    ExpectedReward: habit.Reward,
                    ConfidenceLevel: Math.Round(matchStrength, 3),
                    Reasoning: habit.AutomaticityScore > AutomaticityThreshold
                        ? $"Automatic habit (automaticity: {habit.AutomaticityScore:F2})"
                        : $"Developing habit (automaticity: {habit.AutomaticityScore:F2})");
            }
        }

        return Task.FromResult(bestSuggestion);
    }

    /// <summary>
    /// Returns whether a routine pattern has become automatic (automaticity > 0.85).
    /// </summary>
    /// <param name="routinePattern">The routine to check.</param>
    /// <returns>True if any habit with this routine is automatic.</returns>
    public bool IsAutomatic(string routinePattern)
    {
        ArgumentNullException.ThrowIfNull(routinePattern);
        return _habits.Values.Any(h =>
            h.Routine.Contains(routinePattern, StringComparison.OrdinalIgnoreCase) &&
            h.AutomaticityScore > AutomaticityThreshold);
    }

    /// <summary>
    /// Returns all formed habits sorted by automaticity (highest first).
    /// </summary>
    public IReadOnlyList<Habit> GetFormedHabits()
    {
        return _habits.Values
            .OrderByDescending(h => h.AutomaticityScore)
            .ToList();
    }

    /// <summary>
    /// Returns habits that have achieved automaticity (score > 0.85).
    /// </summary>
    public IReadOnlyList<Habit> GetAutomaticHabits()
    {
        return _habits.Values
            .Where(h => h.AutomaticityScore > AutomaticityThreshold)
            .OrderByDescending(h => h.AutomaticityScore)
            .ToList();
    }

    /// <summary>
    /// Returns the total number of tracked habits.
    /// </summary>
    public int TotalHabits => _habits.Count;

    /// <summary>
    /// Calculates automaticity using the asymptotic formula:
    /// automaticity = 1 - exp(-0.05 * repetitions)
    /// </summary>
    private static double CalculateAutomaticity(int repetitions)
    {
        return Math.Round(1.0 - Math.Exp(-AutomaticityRate * repetitions), 4);
    }

    private static double CalculateCueMatch(string context, string cue)
    {
        var contextWords = context.Split([' ', ',', '.', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length > 2)
            .ToHashSet();

        var cueWords = cue.Split([' ', ',', '.', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length > 2)
            .ToHashSet();

        if (cueWords.Count == 0)
            return 0.0;

        // Exact substring match is strongest
        if (context.Contains(cue, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        // Keyword overlap
        int matches = cueWords.Intersect(contextWords).Count();
        return (double)matches / cueWords.Count;
    }

    private static string BuildKey(string cue, string routine)
    {
        return $"{cue.ToLowerInvariant().Trim()}::{routine.ToLowerInvariant().Trim()}";
    }

    private void EnforceCapacity()
    {
        if (_habits.Count < MaxHabits)
            return;

        // Prune lowest-quality habits
        var toPrune = _habits
            .OrderBy(kvp => kvp.Value.AverageQuality)
            .ThenBy(kvp => kvp.Value.RepetitionCount)
            .Take(10)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string key in toPrune)
            _habits.TryRemove(key, out _);
    }
}

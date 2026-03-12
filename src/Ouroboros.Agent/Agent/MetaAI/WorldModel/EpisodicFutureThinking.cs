// <copyright file="EpisodicFutureThinking.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Implements constructive episodic simulation (Schacter et al., 2007).
/// Combines episodic memory fragments with world model predictions to
/// construct vivid, emotionally-colored future episodes. Uses the
/// <see cref="PredictiveProcessingEngine"/> for probability estimates
/// and prediction error feedback.
/// </summary>
public sealed class EpisodicFutureThinking
{
    /// <summary>A simulated future episode with predicted events and emotional tone.</summary>
    /// <param name="Scenario">The scenario that was simulated.</param>
    /// <param name="PredictedEvents">Ordered list of predicted events.</param>
    /// <param name="EmotionalTone">Dominant emotional tone (e.g., "optimistic", "anxious").</param>
    /// <param name="Confidence">Overall confidence in the simulation [0, 1].</param>
    /// <param name="TimeHorizon">How far into the future this episode projects.</param>
    public sealed record FutureEpisode(
        string Scenario,
        List<PredictedEvent> PredictedEvents,
        string EmotionalTone,
        double Confidence,
        TimeSpan TimeHorizon);

    /// <summary>A single predicted event within a future episode.</summary>
    /// <param name="Description">What is predicted to happen.</param>
    /// <param name="Probability">Estimated probability of occurrence [0, 1].</param>
    /// <param name="EmotionalValence">Emotional valence from -1 (negative) to +1 (positive).</param>
    /// <param name="TimeOffset">Relative time offset from simulation start.</param>
    public sealed record PredictedEvent(
        string Description,
        double Probability,
        double EmotionalValence,
        TimeSpan TimeOffset);

    private readonly PredictiveProcessingEngine _predictionEngine;
    private readonly List<string> _memoryFragments = [];
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodicFutureThinking"/> class.
    /// </summary>
    /// <param name="predictionEngine">The predictive processing engine for probability estimates.</param>
    public EpisodicFutureThinking(PredictiveProcessingEngine predictionEngine)
    {
        ArgumentNullException.ThrowIfNull(predictionEngine);
        _predictionEngine = predictionEngine;
    }

    /// <summary>
    /// Adds an episodic memory fragment that can be recombined in future simulations.
    /// </summary>
    /// <param name="fragment">A memory fragment (scene, event, or detail).</param>
    public void AddMemoryFragment(string fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        lock (_lock)
        {
            _memoryFragments.Add(fragment);
        }
    }

    /// <summary>
    /// Simulates a future episode by combining memory fragments with predictions.
    /// Generates a sequence of predicted events with probability estimates from the
    /// predictive processing engine and emotional coloring derived from goal valence.
    /// </summary>
    /// <param name="scenario">The future scenario to simulate.</param>
    /// <param name="timeHorizon">How far into the future to project.</param>
    /// <returns>A constructed future episode.</returns>
    public Task<FutureEpisode> SimulateFutureEpisodeAsync(string scenario, TimeSpan timeHorizon)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var prediction = _predictionEngine.GeneratePrediction(
            scenario, PredictiveProcessingEngine.PredictionLevel.Strategic);

        List<string> relevantFragments;
        lock (_lock)
        {
            relevantFragments = _memoryFragments
                .Where(f => f.Contains(scenario.Split(' ').FirstOrDefault() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                         || scenario.Contains(f.Split(' ').FirstOrDefault() ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();
        }

        var events = new List<PredictedEvent>();
        var stepCount = Math.Max(2, (int)(timeHorizon.TotalHours / 2) + 1);
        stepCount = Math.Min(stepCount, 10);

        for (int i = 0; i < stepCount; i++)
        {
            var fraction = (double)(i + 1) / stepCount;
            var offset = TimeSpan.FromTicks((long)(timeHorizon.Ticks * fraction));

            // Probability decays with temporal distance
            var baseProbability = prediction.Precision * (1.0 - (fraction * 0.4));

            // Incorporate memory fragments for richer descriptions
            var fragmentBonus = relevantFragments.Count > i
                ? $" (echoing: {relevantFragments[i].Truncate(50)})"
                : string.Empty;

            var description = $"Step {i + 1}: {scenario} progresses{fragmentBonus}";
            var valence = ComputeEmotionalValence(scenario, fraction);

            events.Add(new PredictedEvent(description, Math.Clamp(baseProbability, 0, 1), valence, offset));
        }

        var overallConfidence = events.Count > 0
            ? events.Average(e => e.Probability)
            : prediction.Precision;

        var tone = DetermineEmotionalTone(events);

        var episode = new FutureEpisode(scenario, events, tone, overallConfidence, timeHorizon);
        return Task.FromResult(episode);
    }

    /// <summary>
    /// Generates a vivid first-person future narrative from a goal and step count.
    /// Constructs an emotionally-colored personal future by chaining predictions.
    /// </summary>
    /// <param name="goal">The personal goal to project toward.</param>
    /// <param name="steps">Number of narrative steps to generate.</param>
    /// <returns>A first-person narrative string describing the imagined future.</returns>
    public async Task<string> GeneratePersonalFutureAsync(string goal, int steps)
    {
        ArgumentNullException.ThrowIfNull(goal);
        steps = Math.Clamp(steps, 1, 20);

        var horizon = TimeSpan.FromDays(steps * 7);
        var episode = await SimulateFutureEpisodeAsync(goal, horizon).ConfigureAwait(false);

        var narrative = new StringBuilder();
        narrative.AppendLine($"I imagine myself working toward: {goal}");
        narrative.AppendLine();

        foreach (var ev in episode.PredictedEvents)
        {
            var moodWord = ev.EmotionalValence >= 0.3 ? "hopeful"
                : ev.EmotionalValence <= -0.3 ? "uncertain"
                : "steady";

            narrative.AppendLine($"  [{ev.TimeOffset.Days}d] Feeling {moodWord}: {ev.Description} " +
                                $"(confidence: {ev.Probability:P0})");
        }

        narrative.AppendLine();
        narrative.AppendLine($"Overall tone: {episode.EmotionalTone} | " +
                            $"Confidence: {episode.Confidence:P0}");

        return narrative.ToString();
    }

    /// <summary>
    /// Computes emotional valence for a simulation step.
    /// Positive goal words increase valence; negative words decrease it.
    /// Valence attenuates with temporal distance (less certain = less emotional).
    /// </summary>
    private static double ComputeEmotionalValence(string scenario, double temporalFraction)
    {
        var positiveSignals = new[] { "success", "achieve", "grow", "improve", "learn", "create", "build" };
        var negativeSignals = new[] { "fail", "lose", "risk", "threat", "danger", "problem", "conflict" };

        var lower = scenario.ToLowerInvariant();
        var positiveCount = positiveSignals.Count(s => lower.Contains(s, StringComparison.Ordinal));
        var negativeCount = negativeSignals.Count(s => lower.Contains(s, StringComparison.Ordinal));

        var baseValence = (positiveCount - negativeCount) * 0.3;
        baseValence = Math.Clamp(baseValence, -1.0, 1.0);

        // Attenuate with temporal distance
        return baseValence * (1.0 - (temporalFraction * 0.3));
    }

    /// <summary>Determines dominant emotional tone from predicted events.</summary>
    private static string DetermineEmotionalTone(List<PredictedEvent> events)
    {
        if (events.Count == 0)
        {
            return "neutral";
        }

        var avgValence = events.Average(e => e.EmotionalValence);

        return avgValence switch
        {
            > 0.3 => "optimistic",
            > 0.1 => "cautiously hopeful",
            > -0.1 => "neutral",
            > -0.3 => "apprehensive",
            _ => "anxious",
        };
    }
}

/// <summary>String extension helpers for episodic future thinking.</summary>
internal static class EpisodicStringExtensions
{
    /// <summary>Truncates a string to the specified length, appending ellipsis if needed.</summary>
    internal static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }
}

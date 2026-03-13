// <copyright file="TemporalReasoner.Inference.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Globalization;

namespace Ouroboros.Agent.TemporalReasoning;

public sealed partial class TemporalReasoner
{
    /// <summary>
    /// Infers causal relationships from temporal patterns using LLM.
    /// </summary>
    public async Task<Result<IReadOnlyList<CausalRelation>, string>> InferCausalityAsync(
        IReadOnlyList<TemporalEvent> events,
        CancellationToken ct = default)
    {
        if (events == null || events.Count == 0)
        {
            return Result<IReadOnlyList<CausalRelation>, string>.Failure("Events list cannot be null or empty");
        }

        if (this.llm == null)
        {
            // Without LLM, use simple temporal correlation
            IReadOnlyList<CausalRelation> simpleCausality = InferSimpleCausality(events);
            return Result<IReadOnlyList<CausalRelation>, string>.Success(simpleCausality);
        }

        try
        {
            // Build prompt for LLM
            var prompt = BuildCausalInferencePrompt(events);
            var response = await this.llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);

            // Parse causal relations from response
            IReadOnlyList<CausalRelation> causalRelations = ParseCausalRelations(response, events);

            return Result<IReadOnlyList<CausalRelation>, string>.Success(causalRelations);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<CausalRelation>, string>.Failure($"Causal inference failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Predicts future events based on temporal patterns.
    /// </summary>
    public async Task<Result<IReadOnlyList<PredictedEvent>, string>> PredictFutureEventsAsync(
        IReadOnlyList<TemporalEvent> history,
        TimeSpan horizon,
        CancellationToken ct = default)
    {
        if (history == null || history.Count == 0)
        {
            return Result<IReadOnlyList<PredictedEvent>, string>.Failure("History cannot be null or empty");
        }

        if (horizon <= TimeSpan.Zero)
        {
            return Result<IReadOnlyList<PredictedEvent>, string>.Failure("Horizon must be positive");
        }

        try
        {
            if (this.llm != null)
            {
                return await this.PredictWithLLMAsync(history, horizon, ct).ConfigureAwait(false);
            }

            // Fallback to pattern-based prediction
            IReadOnlyList<PredictedEvent> predictions = PredictWithPatterns(history, horizon);
            return Result<IReadOnlyList<PredictedEvent>, string>.Success(predictions);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<PredictedEvent>, string>.Failure($"Prediction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a temporal constraint is satisfiable.
    /// </summary>
    public Task<Result<bool, string>> CheckConstraintSatisfiabilityAsync(
        IReadOnlyList<TemporalConstraint> constraints,
        CancellationToken ct = default)
    {
        if (constraints == null)
        {
            return Task.FromResult(Result<bool, string>.Failure("Constraints cannot be null"));
        }

        try
        {
            // Build constraint graph
            var constraintMap = new Dictionary<(string, string), TemporalRelation>();

            foreach (var constraint in constraints)
            {
                var key = (constraint.EventIdA, constraint.EventIdB);
                if (constraintMap.TryGetValue(key, out var existing))
                {
                    // Check for conflicts
                    if (existing != constraint.RequiredRelation)
                    {
                        return Task.FromResult(Result<bool, string>.Success(false));
                    }
                }
                else
                {
                    constraintMap[key] = constraint.RequiredRelation;
                }
            }

            // Check for consistency using path consistency
            var satisfiable = CheckPathConsistency(constraintMap);

            return Task.FromResult(Result<bool, string>.Success(satisfiable));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(Result<bool, string>.Failure($"Constraint checking failed: {ex.Message}"));
        }
    }

    private static IReadOnlyList<CausalRelation> InferSimpleCausality(IReadOnlyList<TemporalEvent> events)
    {
        var causalRelations = new List<CausalRelation>();
        var sortedEvents = events.OrderBy(e => e.StartTime).ToList();

        // Simple heuristic: events that occur close in time may be causally related
        for (int i = 0; i < sortedEvents.Count - 1; i++)
        {
            for (int j = i + 1; j < Math.Min(i + 3, sortedEvents.Count); j++)
            {
                var timeDiff = (sortedEvents[j].StartTime - sortedEvents[i].StartTime).TotalMinutes;

                // If events are within the causal window and not overlapping, consider potential causality
                if (timeDiff > 0 && timeDiff <= TemporalReasoningConstants.MaxCausalityWindowMinutes)
                {
                    var strength = Math.Max(0.1, 1.0 - (timeDiff / TemporalReasoningConstants.MaxCausalityWindowMinutes));
                    causalRelations.Add(new CausalRelation(
                        sortedEvents[i],
                        sortedEvents[j],
                        strength,
                        "Temporal proximity",
                        Array.Empty<string>()));
                }
            }
        }

        return causalRelations;
    }

    private static string BuildCausalInferencePrompt(IReadOnlyList<TemporalEvent> events)
    {
        var eventDescriptions = events
            .OrderBy(e => e.StartTime)
            .Select((e, i) => $"{i + 1}. [{e.EventType}] {e.Description} at {e.StartTime:yyyy-MM-dd HH:mm}")
            .ToList();

        return $@"Analyze the following sequence of events and identify causal relationships:

Events:
{string.Join("\n", eventDescriptions)}

For each potential causal relationship, provide:
1. Cause event number
2. Effect event number
3. Causal strength (0-1)
4. Mechanism explaining the causality
5. Confounding factors (if any)

Format your response as:
CAUSE: [event_number]
EFFECT: [event_number]
STRENGTH: [0-1]
MECHANISM: [explanation]
CONFOUNDS: [comma-separated factors or 'none']

---

Provide multiple causal relationships if they exist.";
    }

    private static IReadOnlyList<CausalRelation> ParseCausalRelations(string response, IReadOnlyList<TemporalEvent> events)
    {
        var relations = new List<CausalRelation>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int? causeIdx = null;
        int? effectIdx = null;
        double strength = 0.5;
        string mechanism = "Unknown";
        var confoundsBuilder = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("CAUSE:", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = trimmed.Substring("CAUSE:".Length).Trim();
                if (int.TryParse(numStr, out var num) && num > 0 && num <= events.Count)
                {
                    causeIdx = num - 1;
                }
            }
            else if (trimmed.StartsWith("EFFECT:", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = trimmed.Substring("EFFECT:".Length).Trim();
                if (int.TryParse(numStr, out var num) && num > 0 && num <= events.Count)
                {
                    effectIdx = num - 1;
                }
            }
            else if (trimmed.StartsWith("STRENGTH:", StringComparison.OrdinalIgnoreCase))
            {
                var strStr = trimmed.Substring("STRENGTH:".Length).Trim();
                if (double.TryParse(strStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var str))
                {
                    strength = Math.Clamp(str, 0.0, 1.0);
                }
            }
            else if (trimmed.StartsWith("MECHANISM:", StringComparison.OrdinalIgnoreCase))
            {
                mechanism = trimmed.Substring("MECHANISM:".Length).Trim();
            }
            else if (trimmed.StartsWith("CONFOUNDS:", StringComparison.OrdinalIgnoreCase))
            {
                var confoundStr = trimmed.Substring("CONFOUNDS:".Length).Trim();
                if (!confoundStr.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    confoundsBuilder = confoundStr.Split(',').Select(s => s.Trim()).ToList();
                }
            }
            else if (trimmed == "---" && causeIdx.HasValue && effectIdx.HasValue)
            {
                relations.Add(new CausalRelation(
                    events[causeIdx.Value],
                    events[effectIdx.Value],
                    strength,
                    mechanism,
                    confoundsBuilder));

                causeIdx = null;
                effectIdx = null;
                strength = 0.5;
                mechanism = "Unknown";
                confoundsBuilder = new List<string>();
            }
        }

        // Add last relation if exists
        if (causeIdx.HasValue && effectIdx.HasValue)
        {
            relations.Add(new CausalRelation(
                events[causeIdx.Value],
                events[effectIdx.Value],
                strength,
                mechanism,
                confoundsBuilder));
        }

        return relations;
    }

    private async Task<Result<IReadOnlyList<PredictedEvent>, string>> PredictWithLLMAsync(
        IReadOnlyList<TemporalEvent> history,
        TimeSpan horizon,
        CancellationToken ct)
    {
        var prompt = BuildPredictionPrompt(history, horizon);
        var response = await this.llm!.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
        IReadOnlyList<PredictedEvent> predictions = ParsePredictions(response, history);
        return Result<IReadOnlyList<PredictedEvent>, string>.Success(predictions);
    }

    private static string BuildPredictionPrompt(IReadOnlyList<TemporalEvent> history, TimeSpan horizon)
    {
        var eventDescriptions = history
            .OrderBy(e => e.StartTime)
            .Select(e => $"- [{e.EventType}] {e.Description} at {e.StartTime:yyyy-MM-dd HH:mm}")
            .ToList();

        var latestTime = history.Max(e => e.EndTime ?? e.StartTime);
        var targetTime = latestTime + horizon;

        return $@"Based on the following event history, predict future events that may occur by {targetTime:yyyy-MM-dd HH:mm}:

Historical Events:
{string.Join("\n", eventDescriptions)}

Prediction horizon: {horizon.TotalHours:F1} hours from the last event.

For each predicted event, provide:
1. Event type
2. Description
3. Predicted time (as hours from now)
4. Confidence (0-1)
5. Reasoning

Format:
TYPE: [event_type]
DESCRIPTION: [description]
TIME: [hours_from_now]
CONFIDENCE: [0-1]
REASONING: [explanation]
---";
    }

    private static IReadOnlyList<PredictedEvent> ParsePredictions(string response, IReadOnlyList<TemporalEvent> history)
    {
        var predictions = new List<PredictedEvent>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string? eventType = null;
        string? description = null;
        double hoursFromNow = 0;
        double confidence = 0.5;
        string reasoning = string.Empty;

        var latestTime = history.Max(e => e.EndTime ?? e.StartTime);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("TYPE:", StringComparison.OrdinalIgnoreCase))
            {
                eventType = trimmed.Substring("TYPE:".Length).Trim();
            }
            else if (trimmed.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
            {
                description = trimmed.Substring("DESCRIPTION:".Length).Trim();
            }
            else if (trimmed.StartsWith("TIME:", StringComparison.OrdinalIgnoreCase))
            {
                var timeStr = trimmed.Substring("TIME:".Length).Trim();
                if (double.TryParse(timeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var hours))
                {
                    hoursFromNow = hours;
                }
            }
            else if (trimmed.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                var confStr = trimmed.Substring("CONFIDENCE:".Length).Trim();
                if (double.TryParse(confStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var conf))
                {
                    confidence = Math.Clamp(conf, 0.0, 1.0);
                }
            }
            else if (trimmed.StartsWith("REASONING:", StringComparison.OrdinalIgnoreCase))
            {
                reasoning = trimmed.Substring("REASONING:".Length).Trim();
            }
            else if (trimmed == "---" && eventType != null && description != null)
            {
                var predictedTime = latestTime.AddHours(hoursFromNow);
                predictions.Add(new PredictedEvent(
                    eventType,
                    description,
                    predictedTime,
                    confidence,
                    Array.Empty<TemporalEvent>(),
                    reasoning));

                eventType = null;
                description = null;
                hoursFromNow = 0;
                confidence = 0.5;
                reasoning = string.Empty;
            }
        }

        // Add last prediction if exists
        if (eventType != null && description != null)
        {
            var predictedTime = latestTime.AddHours(hoursFromNow);
            predictions.Add(new PredictedEvent(
                eventType,
                description,
                predictedTime,
                confidence,
                Array.Empty<TemporalEvent>(),
                reasoning));
        }

        return predictions;
    }

    private static IReadOnlyList<PredictedEvent> PredictWithPatterns(IReadOnlyList<TemporalEvent> history, TimeSpan horizon)
    {
        var predictions = new List<PredictedEvent>();

        // Group events by type to find patterns
        var groupedEventsByType = history.GroupBy(e => e.EventType).ToList();

        var latestTime = history.Max(e => e.EndTime ?? e.StartTime);
        var targetTime = latestTime + horizon;

        foreach (var group in groupedEventsByType)
        {
            var groupEvents = group.OrderBy(e => e.StartTime).ToList();
            if (groupEvents.Count < 2)
            {
                continue;
            }

            // Calculate average time between events of this type
            var intervals = new List<TimeSpan>();
            for (int i = 1; i < groupEvents.Count; i++)
            {
                intervals.Add(groupEvents[i].StartTime - groupEvents[i - 1].StartTime);
            }

            var avgInterval = TimeSpan.FromTicks((long)intervals.Average(t => t.Ticks));

            // Predict next occurrence
            var lastEvent = groupEvents[groupEvents.Count - 1];
            var predictedTime = (lastEvent.EndTime ?? lastEvent.StartTime) + avgInterval;

            if (predictedTime <= targetTime)
            {
                predictions.Add(new PredictedEvent(
                    group.Key,
                    $"Predicted {group.Key} based on historical pattern (avg interval: {avgInterval.TotalHours:F1}h)",
                    predictedTime,
                    0.6,
                    groupEvents,
                    $"Pattern-based prediction from {groupEvents.Count} historical events"));
            }
        }

        return predictions;
    }

    private static bool CheckPathConsistency(Dictionary<(string, string), TemporalRelation> constraints)
    {
        // Check for direct contradictions
        foreach (var key in constraints.Keys)
        {
            var reverseKey = (key.Item2, key.Item1);
            if (constraints.TryGetValue(reverseKey, out var reverseRelation) &&
                !AreInverseRelations(constraints[key], reverseRelation))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreInverseRelations(TemporalRelation rel1, TemporalRelation rel2)
    {
        return (rel1, rel2) switch
        {
            (TemporalRelation.Before, TemporalRelation.After) => true,
            (TemporalRelation.After, TemporalRelation.Before) => true,
            (TemporalRelation.Contains, TemporalRelation.During) => true,
            (TemporalRelation.During, TemporalRelation.Contains) => true,
            (TemporalRelation.Simultaneous, TemporalRelation.Simultaneous) => true,
            (TemporalRelation.Meets, TemporalRelation.Meets) => true,
            (TemporalRelation.Overlaps, TemporalRelation.Overlaps) => true,
            _ => false,
        };
    }

    /// <summary>
    /// Maps the detailed Allen Interval Algebra relation to the simplified canonical TemporalRelation.
    /// </summary>
    private static TemporalRelation MapToTemporalRelation(TemporalRelationType relationType) =>
        relationType switch
        {
            TemporalRelationType.Before => TemporalRelation.Before,
            TemporalRelationType.After => TemporalRelation.After,
            TemporalRelationType.Meets or TemporalRelationType.MetBy => TemporalRelation.Meets,
            TemporalRelationType.Overlaps or TemporalRelationType.OverlappedBy => TemporalRelation.Overlaps,
            TemporalRelationType.During => TemporalRelation.During,
            TemporalRelationType.Contains => TemporalRelation.Contains,
            TemporalRelationType.Equals => TemporalRelation.Simultaneous,
            TemporalRelationType.Starts or TemporalRelationType.StartedBy or
            TemporalRelationType.Finishes or TemporalRelationType.FinishedBy => TemporalRelation.Overlaps,
            _ => TemporalRelation.Unknown,
        };
}

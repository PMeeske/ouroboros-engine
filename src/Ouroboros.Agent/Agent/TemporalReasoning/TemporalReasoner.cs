// <copyright file="TemporalReasoner.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Globalization;

namespace Ouroboros.Agent.TemporalReasoning;

/// <summary>
/// Implementation of temporal reasoning engine.
/// Enables reasoning about time, sequences, causality, and temporal relationships between events.
/// </summary>
public sealed partial class TemporalReasoner : ITemporalReasoner
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel? llm;
    private readonly ConcurrentDictionary<Guid, TemporalEvent> eventStore = new();
    private readonly ConcurrentDictionary<string, List<TemporalEvent>> eventsByType = new();
    private readonly ConcurrentDictionary<(Guid, Guid), TemporalRelationType> relationCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporalReasoner"/> class.
    /// </summary>
    /// <param name="llm">Optional LLM for causal inference and pattern recognition.</param>
    public TemporalReasoner(Ouroboros.Abstractions.Core.IChatCompletionModel? llm = null)
    {
        this.llm = llm;
    }

    /// <summary>
    /// Constructs a timeline from a set of events.
    /// NOTE: This method maintains state - events are stored in the reasoner's internal event store for future queries.
    /// The same event can be added multiple times if ConstructTimeline is called multiple times.
    /// </summary>
    public Result<Timeline, string> ConstructTimeline(IReadOnlyList<TemporalEvent> events)
    {
        if (events == null || events.Count == 0)
        {
            return Result<Timeline, string>.Failure("Events list cannot be null or empty");
        }

        try
        {
            // Store events
            foreach (var evt in events)
            {
                this.eventStore[evt.Id] = evt;

                if (!this.eventsByType.ContainsKey(evt.EventType))
                {
                    this.eventsByType[evt.EventType] = new List<TemporalEvent>();
                }

                this.eventsByType[evt.EventType].Add(evt);
            }

            // Sort events by start time
            var sortedEvents = events.OrderBy(e => e.StartTime).ToList();

            // Find earliest and latest times
            var earliestTime = sortedEvents.Min(e => e.StartTime);
            var latestTime = sortedEvents.Max(e => e.EndTime ?? e.StartTime);

            // Compute temporal relations between adjacent events
            var relations = new List<TemporalRelationEdge>();
            for (int i = 0; i < sortedEvents.Count - 1; i++)
            {
                for (int j = i + 1; j < Math.Min(i + 1 + TemporalReasoningConstants.MaxRelationLookahead, sortedEvents.Count); j++)
                {
                    var relType = ComputeAllenRelation(sortedEvents[i], sortedEvents[j]);
                    relations.Add(new TemporalRelationEdge(sortedEvents[i], sortedEvents[j], relType, 1.0));
                }
            }

            // Group events by type
            var groupedByType = sortedEvents
                .GroupBy(e => e.EventType)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<TemporalEvent>)g.ToList());

            var timeline = new Timeline(
                sortedEvents,
                relations,
                earliestTime,
                latestTime,
                groupedByType);

            return Result<Timeline, string>.Success(timeline);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<Timeline, string>.Failure($"Timeline construction failed: {ex.Message}");
        }
    }

}

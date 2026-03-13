// <copyright file="TemporalReasoner.Events.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.TemporalReasoning;

public sealed partial class TemporalReasoner
{
    /// <summary>
    /// Determines temporal relationship between two events using Allen Interval Algebra.
    /// </summary>
    public Task<Result<TemporalRelation, string>> GetRelationAsync(
        TemporalEvent event1,
        TemporalEvent event2,
        CancellationToken ct = default)
    {
        if (event1 == null)
        {
            return Task.FromResult(Result<TemporalRelation, string>.Failure("Event1 cannot be null"));
        }

        if (event2 == null)
        {
            return Task.FromResult(Result<TemporalRelation, string>.Failure("Event2 cannot be null"));
        }

        try
        {
            // Check cache
            var cacheKey = (event1.Id, event2.Id);
            if (this.relationCache.TryGetValue(cacheKey, out var cachedRelation))
            {
                return Task.FromResult(Result<TemporalRelation, string>.Success(MapToTemporalRelation(cachedRelation)));
            }

            // Compute Allen interval relation
            var relationType = ComputeAllenRelation(event1, event2);

            // Cache the result
            this.relationCache[cacheKey] = relationType;

            return Task.FromResult(Result<TemporalRelation, string>.Success(MapToTemporalRelation(relationType)));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(Result<TemporalRelation, string>.Failure($"Failed to compute relation: {ex.Message}"));
        }
    }

    /// <summary>
    /// Queries events matching temporal constraints.
    /// </summary>
    public Task<Result<IReadOnlyList<TemporalEvent>, string>> QueryEventsAsync(
        TemporalQuery query,
        CancellationToken ct = default)
    {
        if (query == null)
        {
            return Task.FromResult(Result<IReadOnlyList<TemporalEvent>, string>.Failure("Query cannot be null"));
        }

        try
        {
            var events = this.eventStore.Values.AsEnumerable();

            // Filter by time range
            if (query.After.HasValue)
            {
                events = events.Where(e => e.StartTime >= query.After.Value);
            }

            if (query.Before.HasValue)
            {
                events = events.Where(e => e.StartTime <= query.Before.Value);
            }

            // Filter by event type
            if (!string.IsNullOrWhiteSpace(query.EventType))
            {
                events = events.Where(e => e.EventType.Equals(query.EventType, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by duration
            if (query.Duration.HasValue)
            {
                events = events.Where(e =>
                {
                    if (e.EndTime.HasValue)
                    {
                        var duration = e.EndTime.Value - e.StartTime;
                        return Math.Abs(duration.TotalSeconds - query.Duration.Value.TotalSeconds) < 1.0;
                    }

                    return false;
                });
            }

            // Filter by related event (temporal proximity)
            if (query.RelatedEventId.HasValue &&
                this.eventStore.TryGetValue(query.RelatedEventId.Value, out var relatedEvent))
            {
                events = events.Where(e =>
                {
                    var relation = ComputeAllenRelation(e, relatedEvent);
                    return relation != TemporalRelationType.Before && relation != TemporalRelationType.After;
                });
            }

            IReadOnlyList<TemporalEvent> results = events.Take(query.MaxResults).ToList();
            return Task.FromResult(Result<IReadOnlyList<TemporalEvent>, string>.Success(results));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(Result<IReadOnlyList<TemporalEvent>, string>.Failure($"Query failed: {ex.Message}"));
        }
    }

    private static TemporalRelationType ComputeAllenRelation(TemporalEvent event1, TemporalEvent event2)
    {
        var start1 = event1.StartTime;
        var end1 = event1.EndTime ?? event1.StartTime;
        var start2 = event2.StartTime;
        var end2 = event2.EndTime ?? event2.StartTime;

        // Equals
        if (start1 == start2 && end1 == end2)
        {
            return TemporalRelationType.Equals;
        }

        // Before / After / Meets / MetBy
        if (end1 <= start2)
        {
            return end1 == start2 ? TemporalRelationType.Meets : TemporalRelationType.Before;
        }

        if (start1 >= end2)
        {
            return start1 == end2 ? TemporalRelationType.MetBy : TemporalRelationType.After;
        }

        // Overlaps / OverlappedBy
        if (start1 < start2 && end1 > start2 && end1 < end2)
        {
            return TemporalRelationType.Overlaps;
        }

        if (start2 < start1 && end2 > start1 && end2 < end1)
        {
            return TemporalRelationType.OverlappedBy;
        }

        // During / Contains
        if (start1 > start2 && end1 < end2)
        {
            return TemporalRelationType.During;
        }

        if (start2 > start1 && end2 < end1)
        {
            return TemporalRelationType.Contains;
        }

        // Starts / StartedBy
        if (start1 == start2)
        {
            return end1 < end2 ? TemporalRelationType.Starts : TemporalRelationType.StartedBy;
        }

        // Finishes / FinishedBy
        if (end1 == end2)
        {
            return start1 > start2 ? TemporalRelationType.Finishes : TemporalRelationType.FinishedBy;
        }

        // Default to Before if no other relation matches
        return TemporalRelationType.Before;
    }
}

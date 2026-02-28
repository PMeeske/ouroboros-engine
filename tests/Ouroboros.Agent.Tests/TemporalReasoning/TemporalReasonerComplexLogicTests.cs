// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using FluentAssertions;
using Moq;
using Ouroboros.Agent.TemporalReasoning;
using Xunit;
using TRTemporalConstraint = Ouroboros.Agent.TemporalReasoning.TemporalConstraint;
using TRTemporalRelation = Ouroboros.Agent.TemporalReasoning.TemporalRelation;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.TemporalReasoning;

/// <summary>
/// Complex-logic tests for TemporalReasoner: Allen interval algebra correctness,
/// causal inference, pattern-based prediction, timeline construction with relations,
/// constraint satisfiability, event query filtering, and LLM-backed inference paths.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TemporalReasonerComplexLogicTests
{
    private static TemporalEvent Evt(
        string type, DateTime start, DateTime? end = null,
        string? description = null)
    {
        return new TemporalEvent(
            Guid.NewGuid(), type,
            description ?? $"Event {type}",
            start, end,
            new Dictionary<string, object>(),
            new List<string>());
    }

    // ========================================================
    // Allen Interval Algebra: all 13 relations
    // ========================================================

    [Fact]
    public async Task GetRelation_Meets_WhenEndOfFirstEqualsStartOfSecond()
    {
        // A: [10:00, 11:00], B: [11:00, 12:00] => A Meets B
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var e1 = Evt("A", now, now.AddHours(1));
        var e2 = Evt("B", now.AddHours(1), now.AddHours(2));

        var reasoner = new TemporalReasoner();
        var result = await reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(TRTemporalRelation.Meets);
    }

    [Fact]
    public async Task GetRelation_MetBy_WhenStartOfFirstEqualsEndOfSecond()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var e1 = Evt("A", now.AddHours(2), now.AddHours(3));
        var e2 = Evt("B", now, now.AddHours(2));

        var reasoner = new TemporalReasoner();
        var result = await reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        // MetBy maps to Meets in the simplified relation
        result.Value.Should().Be(TRTemporalRelation.Meets);
    }

    [Fact]
    public async Task GetRelation_Overlaps_WhenFirstStartsBeforeSecondAndEndsInside()
    {
        // A: [10:00, 11:30], B: [11:00, 12:00]
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var e1 = Evt("A", now, now.AddMinutes(90));
        var e2 = Evt("B", now.AddMinutes(60), now.AddMinutes(120));

        var reasoner = new TemporalReasoner();
        var result = await reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(TRTemporalRelation.Overlaps);
    }

    [Fact]
    public async Task GetRelation_OverlappedBy_WhenSecondStartsBeforeFirstAndEndsInside()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var e1 = Evt("A", now.AddMinutes(60), now.AddMinutes(120));
        var e2 = Evt("B", now, now.AddMinutes(90));

        var reasoner = new TemporalReasoner();
        var result = await reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(TRTemporalRelation.Overlaps); // OverlappedBy maps to Overlaps
    }

    [Fact]
    public async Task GetRelation_During_WhenFirstIsContainedInSecond()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var e1 = Evt("A", now.AddMinutes(30), now.AddMinutes(90));
        var e2 = Evt("B", now, now.AddMinutes(120));

        var reasoner = new TemporalReasoner();
        var result = await reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(TRTemporalRelation.During);
    }

    [Fact]
    public async Task GetRelation_Contains_WhenFirstContainsSecond()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var e1 = Evt("A", now, now.AddMinutes(120));
        var e2 = Evt("B", now.AddMinutes(30), now.AddMinutes(90));

        var reasoner = new TemporalReasoner();
        var result = await reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(TRTemporalRelation.Contains);
    }

    [Fact]
    public async Task GetRelation_Starts_WhenBothStartTogetherFirstEndsSooner()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var e1 = Evt("A", now, now.AddMinutes(30));
        var e2 = Evt("B", now, now.AddMinutes(60));

        var reasoner = new TemporalReasoner();
        var result = await reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        // Starts maps to Overlaps in simplified
        result.Value.Should().Be(TRTemporalRelation.Overlaps);
    }

    [Fact]
    public async Task GetRelation_Finishes_WhenBothEndTogetherFirstStartsLater()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = now.AddMinutes(60);
        var e1 = Evt("A", now.AddMinutes(30), end);
        var e2 = Evt("B", now, end);

        var reasoner = new TemporalReasoner();
        var result = await reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        // Finishes maps to Overlaps in simplified
        result.Value.Should().Be(TRTemporalRelation.Overlaps);
    }

    [Fact]
    public async Task GetRelation_PointEvents_SameTime_ReturnsSimultaneous()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var e1 = Evt("A", now, null);
        var e2 = Evt("B", now, null);

        var reasoner = new TemporalReasoner();
        var result = await reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(TRTemporalRelation.Simultaneous);
    }

    // ========================================================
    // Caching: same pair returns cached result
    // ========================================================

    [Fact]
    public async Task GetRelation_SecondCallUsesCachedResult()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var e1 = Evt("A", now, now.AddHours(1));
        var e2 = Evt("B", now.AddHours(2), now.AddHours(3));

        var reasoner = new TemporalReasoner();
        var result1 = await reasoner.GetRelationAsync(e1, e2);
        var result2 = await reasoner.GetRelationAsync(e1, e2);

        result1.Value.Should().Be(result2.Value);
    }

    // ========================================================
    // InferCausalityAsync: temporal proximity heuristic
    // ========================================================

    [Fact]
    public async Task InferSimpleCausality_CloseEvents_ReturnsCausalRelation()
    {
        // Two events within the 60-minute causal window
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("sensor_alert", now, now.AddMinutes(1)),
            Evt("response_action", now.AddMinutes(10), now.AddMinutes(15)),
        };

        var reasoner = new TemporalReasoner();
        var result = await reasoner.InferCausalityAsync(events);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountGreaterThan(0);
        result.Value[0].Cause.EventType.Should().Be("sensor_alert");
        result.Value[0].Effect.EventType.Should().Be("response_action");
        result.Value[0].CausalStrength.Should().BeGreaterThan(0.0);
        result.Value[0].CausalStrength.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task InferSimpleCausality_CausalStrengthDecreasesWithTimeDifference()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("cause", now, now.AddMinutes(1)),
            Evt("close_effect", now.AddMinutes(5), now.AddMinutes(6)),
            Evt("far_effect", now.AddMinutes(50), now.AddMinutes(51)),
        };

        var reasoner = new TemporalReasoner();
        var result = await reasoner.InferCausalityAsync(events);

        result.IsSuccess.Should().BeTrue();
        // The closer event should have higher causal strength
        var closeRelation = result.Value.FirstOrDefault(
            r => r.Cause.EventType == "cause" && r.Effect.EventType == "close_effect");
        var farRelation = result.Value.FirstOrDefault(
            r => r.Cause.EventType == "cause" && r.Effect.EventType == "far_effect");

        closeRelation.Should().NotBeNull();
        farRelation.Should().NotBeNull();
        closeRelation!.CausalStrength.Should().BeGreaterThan(farRelation!.CausalStrength);
    }

    [Fact]
    public async Task InferSimpleCausality_EventsBeyondWindow_NoCausalRelation()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("A", now, now.AddMinutes(1)),
            Evt("B", now.AddMinutes(120), now.AddMinutes(121)), // 2 hours apart
        };

        var reasoner = new TemporalReasoner();
        var result = await reasoner.InferCausalityAsync(events);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty("events are 120 minutes apart, beyond 60-minute window");
    }

    [Fact]
    public async Task InferSimpleCausality_OnlyLooksAhead3Events()
    {
        // The simple causality only looks ahead at most 3 events
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("A", now, now.AddMinutes(1)),
            Evt("B", now.AddMinutes(5), now.AddMinutes(6)),
            Evt("C", now.AddMinutes(10), now.AddMinutes(11)),
            Evt("D", now.AddMinutes(15), now.AddMinutes(16)),
            Evt("E", now.AddMinutes(20), now.AddMinutes(21)),
        };

        var reasoner = new TemporalReasoner();
        var result = await reasoner.InferCausalityAsync(events);

        result.IsSuccess.Should().BeTrue();
        // A should not have a direct causal link to D or E (only looks +2 ahead => B, C)
        var aToD = result.Value.FirstOrDefault(
            r => r.Cause.EventType == "A" && r.Effect.EventType == "D");
        aToD.Should().BeNull("simple causality only looks ahead up to index i+2");
    }

    // ========================================================
    // InferCausalityAsync with LLM
    // ========================================================

    [Fact]
    public async Task InferCausality_WithLLM_ParsesCausalRelationsFromResponse()
    {
        var llmMock = new Mock<IChatCompletionModel>();
        llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                "CAUSE: 1\nEFFECT: 2\nSTRENGTH: 0.85\nMECHANISM: Direct trigger\nCONFOUNDS: none\n---");

        var reasoner = new TemporalReasoner(llmMock.Object);
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("power_outage", now, now.AddMinutes(5)),
            Evt("alarm_triggered", now.AddMinutes(1), now.AddMinutes(10)),
        };

        var result = await reasoner.InferCausalityAsync(events);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].CausalStrength.Should().BeApproximately(0.85, 0.001);
        result.Value[0].Mechanism.Should().Be("Direct trigger");
    }

    [Fact]
    public async Task InferCausality_WithLLM_ParsesMultipleRelations()
    {
        var llmMock = new Mock<IChatCompletionModel>();
        llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                "CAUSE: 1\nEFFECT: 2\nSTRENGTH: 0.9\nMECHANISM: Trigger\nCONFOUNDS: none\n---\n" +
                "CAUSE: 2\nEFFECT: 3\nSTRENGTH: 0.7\nMECHANISM: Chain reaction\nCONFOUNDS: weather, time\n---");

        var reasoner = new TemporalReasoner(llmMock.Object);
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("A", now, now.AddMinutes(5)),
            Evt("B", now.AddMinutes(10), now.AddMinutes(15)),
            Evt("C", now.AddMinutes(20), now.AddMinutes(25)),
        };

        var result = await reasoner.InferCausalityAsync(events);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[1].ConfoundingFactors.Should().Contain("weather");
        result.Value[1].ConfoundingFactors.Should().Contain("time");
    }

    [Fact]
    public async Task InferCausality_WithLLM_ClampsStrength()
    {
        var llmMock = new Mock<IChatCompletionModel>();
        llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("CAUSE: 1\nEFFECT: 2\nSTRENGTH: 5.0\nMECHANISM: Overflow\nCONFOUNDS: none");

        var reasoner = new TemporalReasoner(llmMock.Object);
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("A", now, now.AddMinutes(5)),
            Evt("B", now.AddMinutes(10), now.AddMinutes(15)),
        };

        var result = await reasoner.InferCausalityAsync(events);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].CausalStrength.Should().BeLessThanOrEqualTo(1.0);
    }

    // ========================================================
    // PredictFutureEventsAsync: pattern-based prediction
    // ========================================================

    [Fact]
    public async Task PredictWithPatterns_RecurringEvents_PredictsContinuation()
    {
        // Events occur every hour, should predict next occurrence
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var history = new List<TemporalEvent>
        {
            Evt("heartbeat", now, now.AddMinutes(5)),
            Evt("heartbeat", now.AddHours(1), now.AddHours(1).AddMinutes(5)),
            Evt("heartbeat", now.AddHours(2), now.AddHours(2).AddMinutes(5)),
        };

        var reasoner = new TemporalReasoner();
        var result = await reasoner.PredictFutureEventsAsync(
            history, TimeSpan.FromHours(2));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountGreaterThan(0);
        result.Value[0].EventType.Should().Be("heartbeat");
        result.Value[0].Confidence.Should().Be(0.6);
    }

    [Fact]
    public async Task PredictWithPatterns_SingleEvent_NoPrediction()
    {
        // Need at least 2 events of same type for pattern detection
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var history = new List<TemporalEvent>
        {
            Evt("unique_event", now, now.AddMinutes(5)),
        };

        var reasoner = new TemporalReasoner();
        var result = await reasoner.PredictFutureEventsAsync(
            history, TimeSpan.FromHours(24));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty("single events cannot produce patterns");
    }

    [Fact]
    public async Task PredictWithPatterns_PredictedTimeBeyondHorizon_NoPrediction()
    {
        // Events 24 hours apart, horizon only 1 hour => prediction beyond horizon
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var history = new List<TemporalEvent>
        {
            Evt("daily_report", now, now.AddMinutes(5)),
            Evt("daily_report", now.AddDays(1), now.AddDays(1).AddMinutes(5)),
        };

        var reasoner = new TemporalReasoner();
        var result = await reasoner.PredictFutureEventsAsync(
            history, TimeSpan.FromHours(1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty("next occurrence is 24h away, beyond 1h horizon");
    }

    [Fact]
    public async Task PredictWithPatterns_MultipleEventTypes_PredictsSeparately()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var history = new List<TemporalEvent>
        {
            Evt("typeA", now, now.AddMinutes(5)),
            Evt("typeA", now.AddHours(1), now.AddHours(1).AddMinutes(5)),
            Evt("typeB", now.AddMinutes(30), now.AddMinutes(35)),
            Evt("typeB", now.AddHours(1).AddMinutes(30), now.AddHours(1).AddMinutes(35)),
        };

        var reasoner = new TemporalReasoner();
        var result = await reasoner.PredictFutureEventsAsync(
            history, TimeSpan.FromHours(2));

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(p => p.EventType).Should().Contain("typeA");
        result.Value.Select(p => p.EventType).Should().Contain("typeB");
    }

    [Fact]
    public async Task PredictFutureEvents_ZeroHorizon_ReturnsFailure()
    {
        var events = new List<TemporalEvent>
        {
            Evt("A", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)),
        };

        var reasoner = new TemporalReasoner();
        var result = await reasoner.PredictFutureEventsAsync(events, TimeSpan.Zero);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("positive");
    }

    // ========================================================
    // PredictWithLLM
    // ========================================================

    [Fact]
    public async Task PredictWithLLM_ParsesPredictionsFromResponse()
    {
        var llmMock = new Mock<IChatCompletionModel>();
        llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                "TYPE: failure\nDESCRIPTION: System failure predicted\n" +
                "TIME: 2.5\nCONFIDENCE: 0.8\nREASONING: Pattern detected\n---");

        var reasoner = new TemporalReasoner(llmMock.Object);
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var history = new List<TemporalEvent>
        {
            Evt("alert", now, now.AddMinutes(5)),
            Evt("warning", now.AddMinutes(30), now.AddMinutes(35)),
        };

        var result = await reasoner.PredictFutureEventsAsync(
            history, TimeSpan.FromHours(5));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].EventType.Should().Be("failure");
        result.Value[0].Confidence.Should().BeApproximately(0.8, 0.001);
    }

    // ========================================================
    // ConstructTimeline: sorting, relations, grouping
    // ========================================================

    [Fact]
    public void ConstructTimeline_SortsEventsByStartTime()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("C", now.AddHours(2), now.AddHours(3)),
            Evt("A", now, now.AddHours(1)),
            Evt("B", now.AddHours(1), now.AddHours(2)),
        };

        var reasoner = new TemporalReasoner();
        var result = reasoner.ConstructTimeline(events);

        result.IsSuccess.Should().BeTrue();
        result.Value.Events.Select(e => e.EventType)
            .Should().ContainInOrder("A", "B", "C");
    }

    [Fact]
    public void ConstructTimeline_ComputesRelationsBetweenAdjacentEvents()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("A", now, now.AddHours(1)),
            Evt("B", now.AddHours(2), now.AddHours(3)),
            Evt("C", now.AddHours(4), now.AddHours(5)),
        };

        var reasoner = new TemporalReasoner();
        var result = reasoner.ConstructTimeline(events);

        result.IsSuccess.Should().BeTrue();
        result.Value.Relations.Should().NotBeEmpty();
    }

    [Fact]
    public void ConstructTimeline_ComputesEarliestAndLatestTimes()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("A", now, now.AddHours(1)),
            Evt("B", now.AddHours(5), now.AddHours(10)),
        };

        var reasoner = new TemporalReasoner();
        var result = reasoner.ConstructTimeline(events);

        result.IsSuccess.Should().BeTrue();
        result.Value.EarliestTime.Should().Be(now);
        result.Value.LatestTime.Should().Be(now.AddHours(10));
    }

    [Fact]
    public void ConstructTimeline_GroupsEventsByType()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("sensor", now, now.AddMinutes(5)),
            Evt("alarm", now.AddMinutes(10), now.AddMinutes(15)),
            Evt("sensor", now.AddMinutes(20), now.AddMinutes(25)),
        };

        var reasoner = new TemporalReasoner();
        var result = reasoner.ConstructTimeline(events);

        result.IsSuccess.Should().BeTrue();
        result.Value.EventsByType.Should().ContainKey("sensor");
        result.Value.EventsByType["sensor"].Should().HaveCount(2);
        result.Value.EventsByType.Should().ContainKey("alarm");
        result.Value.EventsByType["alarm"].Should().HaveCount(1);
    }

    [Fact]
    public void ConstructTimeline_StoresEventsInternallyForQueries()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var events = new List<TemporalEvent>
        {
            Evt("test", now, now.AddHours(1)),
        };

        var reasoner = new TemporalReasoner();
        reasoner.ConstructTimeline(events);

        // Should be able to query stored events
        var queryResult = reasoner.QueryEventsAsync(
            new TemporalQuery(EventType: "test")).Result;
        queryResult.IsSuccess.Should().BeTrue();
        queryResult.Value.Should().HaveCount(1);
    }

    // ========================================================
    // QueryEventsAsync: filtering
    // ========================================================

    [Fact]
    public async Task QueryEvents_FilterByTimeRange()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var reasoner = new TemporalReasoner();
        reasoner.ConstructTimeline(new List<TemporalEvent>
        {
            Evt("A", now, now.AddHours(1)),
            Evt("B", now.AddHours(5), now.AddHours(6)),
            Evt("C", now.AddHours(10), now.AddHours(11)),
        });

        var result = await reasoner.QueryEventsAsync(new TemporalQuery(
            After: now.AddHours(4),
            Before: now.AddHours(7)));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].EventType.Should().Be("B");
    }

    [Fact]
    public async Task QueryEvents_FilterByEventType_CaseInsensitive()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var reasoner = new TemporalReasoner();
        reasoner.ConstructTimeline(new List<TemporalEvent>
        {
            Evt("Sensor", now, now.AddHours(1)),
            Evt("Alarm", now.AddHours(2), now.AddHours(3)),
            Evt("Sensor", now.AddHours(4), now.AddHours(5)),
        });

        var result = await reasoner.QueryEventsAsync(new TemporalQuery(EventType: "sensor"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryEvents_FilterByDuration()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var reasoner = new TemporalReasoner();
        reasoner.ConstructTimeline(new List<TemporalEvent>
        {
            Evt("short", now, now.AddMinutes(5)),
            Evt("long", now.AddHours(1), now.AddHours(2)),
            Evt("exact", now.AddHours(3), now.AddHours(3).AddMinutes(30)),
        });

        var result = await reasoner.QueryEventsAsync(new TemporalQuery(
            Duration: TimeSpan.FromMinutes(30)));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].EventType.Should().Be("exact");
    }

    [Fact]
    public async Task QueryEvents_MaxResultsLimitsOutput()
    {
        var now = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var reasoner = new TemporalReasoner();
        var events = Enumerable.Range(0, 20)
            .Select(i => Evt("type", now.AddMinutes(i * 10), now.AddMinutes(i * 10 + 5)))
            .ToList();
        reasoner.ConstructTimeline(events);

        var result = await reasoner.QueryEventsAsync(new TemporalQuery(MaxResults: 5));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(5);
    }

    // ========================================================
    // CheckConstraintSatisfiabilityAsync
    // ========================================================

    [Fact]
    public async Task CheckConstraints_ConsistentBeforeAfter_ReturnsTrue()
    {
        var reasoner = new TemporalReasoner();
        var constraints = new List<TRTemporalConstraint>
        {
            new("A", "B", TRTemporalRelation.Before),
            new("B", "A", TRTemporalRelation.After),
        };

        var result = await reasoner.CheckConstraintSatisfiabilityAsync(constraints);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConstraints_ConflictingRelations_ReturnsFalse()
    {
        var reasoner = new TemporalReasoner();
        var constraints = new List<TRTemporalConstraint>
        {
            new("A", "B", TRTemporalRelation.Before),
            new("A", "B", TRTemporalRelation.After),
        };

        var result = await reasoner.CheckConstraintSatisfiabilityAsync(constraints);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse("same pair with conflicting relations");
    }

    [Fact]
    public async Task CheckConstraints_SimultaneousWithItself_ReturnsTrue()
    {
        var reasoner = new TemporalReasoner();
        var constraints = new List<TRTemporalConstraint>
        {
            new("A", "B", TRTemporalRelation.Simultaneous),
            new("B", "A", TRTemporalRelation.Simultaneous),
        };

        var result = await reasoner.CheckConstraintSatisfiabilityAsync(constraints);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue("simultaneous is its own inverse");
    }

    [Fact]
    public async Task CheckConstraints_InconsistentInverses_ReturnsFalse()
    {
        var reasoner = new TemporalReasoner();
        var constraints = new List<TRTemporalConstraint>
        {
            new("A", "B", TRTemporalRelation.Before),
            new("B", "A", TRTemporalRelation.Before), // Should be After
        };

        var result = await reasoner.CheckConstraintSatisfiabilityAsync(constraints);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse("Before-Before is not a valid inverse pair");
    }
}

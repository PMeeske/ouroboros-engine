// <copyright file="TemporalReasonerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.TemporalReasoning;
using TRTemporalConstraint = Ouroboros.Agent.TemporalReasoning.TemporalConstraint;
using TRTemporalRelation = Ouroboros.Agent.TemporalReasoning.TemporalRelation;

namespace Ouroboros.Tests.TemporalReasoning;

[Trait("Category", "Unit")]
public class TemporalReasonerTests
{
    private readonly TemporalReasoner _reasoner = new();

    [Fact]
    public async Task GetRelationAsync_NullEvent1_ReturnsFailure()
    {
        var e2 = CreateEvent("A", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        var result = await _reasoner.GetRelationAsync(null!, e2);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetRelationAsync_NullEvent2_ReturnsFailure()
    {
        var e1 = CreateEvent("A", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        var result = await _reasoner.GetRelationAsync(e1, null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetRelationAsync_BeforeRelation()
    {
        var e1 = CreateEvent("A", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        var e2 = CreateEvent("B", DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(3));

        var result = await _reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(TRTemporalRelation.Before);
    }

    [Fact]
    public async Task GetRelationAsync_AfterRelation()
    {
        var e1 = CreateEvent("A", DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(3));
        var e2 = CreateEvent("B", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        var result = await _reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(TRTemporalRelation.After);
    }

    [Fact]
    public async Task GetRelationAsync_SimultaneousRelation()
    {
        var now = DateTime.UtcNow;
        var end = now.AddHours(1);
        var e1 = CreateEvent("A", now, end);
        var e2 = CreateEvent("B", now, end);

        var result = await _reasoner.GetRelationAsync(e1, e2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(TRTemporalRelation.Simultaneous);
    }

    [Fact]
    public async Task QueryEventsAsync_NullQuery_ReturnsFailure()
    {
        var result = await _reasoner.QueryEventsAsync(null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ConstructTimeline_NullEvents_ReturnsFailure()
    {
        var result = _reasoner.ConstructTimeline(null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ConstructTimeline_EmptyEvents_ReturnsFailure()
    {
        var result = _reasoner.ConstructTimeline(new List<TemporalEvent>());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ConstructTimeline_ValidEvents_ReturnsTimeline()
    {
        var now = DateTime.UtcNow;
        var events = new List<TemporalEvent>
        {
            CreateEvent("A", now, now.AddHours(1)),
            CreateEvent("B", now.AddHours(2), now.AddHours(3)),
        };

        var result = _reasoner.ConstructTimeline(events);

        result.IsSuccess.Should().BeTrue();
        result.Value.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task InferCausalityAsync_NullEvents_ReturnsFailure()
    {
        var result = await _reasoner.InferCausalityAsync(null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task InferCausalityAsync_EmptyEvents_ReturnsFailure()
    {
        var result = await _reasoner.InferCausalityAsync(new List<TemporalEvent>());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task InferCausalityAsync_WithoutLlm_UsesSimpleCausality()
    {
        var now = DateTime.UtcNow;
        var events = new List<TemporalEvent>
        {
            CreateEvent("A", now, now.AddMinutes(5)),
            CreateEvent("B", now.AddMinutes(10), now.AddMinutes(15)),
        };

        var result = await _reasoner.InferCausalityAsync(events);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PredictFutureEventsAsync_EmptyHistory_ReturnsFailure()
    {
        var result = await _reasoner.PredictFutureEventsAsync(new List<TemporalEvent>(), TimeSpan.FromHours(1));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PredictFutureEventsAsync_NegativeHorizon_ReturnsFailure()
    {
        var events = new List<TemporalEvent>
        {
            CreateEvent("A", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)),
        };

        var result = await _reasoner.PredictFutureEventsAsync(events, TimeSpan.FromHours(-1));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConstraintSatisfiabilityAsync_NullConstraints_ReturnsFailure()
    {
        var result = await _reasoner.CheckConstraintSatisfiabilityAsync(null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConstraintSatisfiabilityAsync_EmptyConstraints_ReturnsTrue()
    {
        var result = await _reasoner.CheckConstraintSatisfiabilityAsync(new List<TRTemporalConstraint>());
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    private static TemporalEvent CreateEvent(string type, DateTime start, DateTime? end = null)
    {
        return new TemporalEvent(
            Guid.NewGuid(),
            type,
            $"Event {type}",
            start,
            end,
            new Dictionary<string, object>(),
            new List<string>());
    }
}

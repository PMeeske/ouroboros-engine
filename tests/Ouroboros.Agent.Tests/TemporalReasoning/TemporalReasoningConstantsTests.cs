// <copyright file="TemporalReasoningConstantsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.TemporalReasoning;

namespace Ouroboros.Tests.TemporalReasoning;

/// <summary>
/// Unit tests for <see cref="TemporalReasoningConstants"/>.
/// Verifies that configuration constants maintain expected values
/// and reasonable bounds for temporal reasoning heuristics.
/// </summary>
[Trait("Category", "Unit")]
public class TemporalReasoningConstantsTests
{
    [Fact]
    public void MaxRelationLookahead_HasExpectedValue()
    {
        TemporalReasoningConstants.MaxRelationLookahead.Should().Be(5);
    }

    [Fact]
    public void MaxRelationLookahead_IsPositive()
    {
        TemporalReasoningConstants.MaxRelationLookahead.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MaxCausalityWindowMinutes_HasExpectedValue()
    {
        TemporalReasoningConstants.MaxCausalityWindowMinutes.Should().Be(60.0);
    }

    [Fact]
    public void MaxCausalityWindowMinutes_IsPositive()
    {
        TemporalReasoningConstants.MaxCausalityWindowMinutes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MaxCausalityWindowMinutes_IsReasonableBound()
    {
        // The causality window should be at most a few hours for temporal reasoning
        TemporalReasoningConstants.MaxCausalityWindowMinutes.Should().BeLessThanOrEqualTo(1440.0); // 24 hours
    }
}

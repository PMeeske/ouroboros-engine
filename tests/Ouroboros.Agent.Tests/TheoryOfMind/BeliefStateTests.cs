// <copyright file="BeliefStateTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.TheoryOfMind;

namespace Ouroboros.Tests.TheoryOfMind;

[Trait("Category", "Unit")]
public sealed class BeliefStateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var beliefs = new Dictionary<string, BeliefValue>
        {
            ["key1"] = new BeliefValue("prop1", 0.9, "observation")
        };
        var now = DateTime.UtcNow;

        // Act
        var state = new BeliefState("agent-1", beliefs, 0.8, now);

        // Assert
        state.AgentId.Should().Be("agent-1");
        state.Beliefs.Should().HaveCount(1);
        state.Confidence.Should().Be(0.8);
        state.LastUpdated.Should().Be(now);
    }

    [Fact]
    public void Empty_CreatesStateWithNoBeliefs()
    {
        // Act
        var state = BeliefState.Empty("agent-1");

        // Assert
        state.AgentId.Should().Be("agent-1");
        state.Beliefs.Should().BeEmpty();
        state.Confidence.Should().Be(0.0);
        state.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void WithBelief_AddsNewBelief()
    {
        // Arrange
        var state = BeliefState.Empty("agent-1");
        var belief = new BeliefValue("wants_help", 0.9, "observation");

        // Act
        var updated = state.WithBelief("help", belief);

        // Assert
        updated.Beliefs.Should().ContainKey("help");
        updated.Beliefs["help"].Should().Be(belief);
        updated.LastUpdated.Should().BeOnOrAfter(state.LastUpdated);
    }

    [Fact]
    public void WithBelief_UpdatesExistingBelief()
    {
        // Arrange
        var original = new BeliefValue("wants_help", 0.5, "inference");
        var state = BeliefState.Empty("agent-1").WithBelief("help", original);
        var updated = new BeliefValue("wants_help", 0.9, "observation");

        // Act
        var result = state.WithBelief("help", updated);

        // Assert
        result.Beliefs["help"].Probability.Should().Be(0.9);
        result.Beliefs["help"].Source.Should().Be("observation");
    }

    [Fact]
    public void WithBelief_DoesNotMutateOriginal()
    {
        // Arrange
        var state = BeliefState.Empty("agent-1");
        var belief = new BeliefValue("prop", 0.7, "inference");

        // Act
        _ = state.WithBelief("key", belief);

        // Assert
        state.Beliefs.Should().BeEmpty();
    }

    [Fact]
    public void WithConfidence_ClampsToValidRange()
    {
        // Arrange
        var state = BeliefState.Empty("agent-1");

        // Act & Assert
        state.WithConfidence(1.5).Confidence.Should().Be(1.0);
        state.WithConfidence(-0.5).Confidence.Should().Be(0.0);
        state.WithConfidence(0.7).Confidence.Should().Be(0.7);
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var beliefs = new Dictionary<string, BeliefValue>();
        var a = new BeliefState("agent-1", beliefs, 0.5, now);
        var b = new BeliefState("agent-1", beliefs, 0.5, now);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var state = BeliefState.Empty("agent-1");

        // Act
        var modified = state with { Confidence = 0.99 };

        // Assert
        modified.Confidence.Should().Be(0.99);
        modified.AgentId.Should().Be("agent-1");
    }
}

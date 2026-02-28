// <copyright file="AgentObservationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.TheoryOfMind;

namespace Ouroboros.Tests.TheoryOfMind;

/// <summary>
/// Unit tests for the <see cref="AgentObservation"/> record.
/// Covers constructors, factory methods, context handling, and record equality.
/// </summary>
[Trait("Category", "Unit")]
public class AgentObservationTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var context = new Dictionary<string, object> { ["key"] = "value" };
        var timestamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var observation = new AgentObservation("agent-1", "action", "moved forward", context, timestamp);

        // Assert
        observation.AgentId.Should().Be("agent-1");
        observation.ObservationType.Should().Be("action");
        observation.Content.Should().Be("moved forward");
        observation.Context.Should().ContainKey("key");
        observation.ObservedAt.Should().Be(timestamp);
    }

    [Fact]
    public void Action_Factory_SetsTypeToAction()
    {
        // Act
        var observation = AgentObservation.Action("agent-1", "picked up object");

        // Assert
        observation.AgentId.Should().Be("agent-1");
        observation.ObservationType.Should().Be("action");
        observation.Content.Should().Be("picked up object");
        observation.ObservedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Action_Factory_NullContext_DefaultsToEmptyDictionary()
    {
        // Act
        var observation = AgentObservation.Action("agent-1", "jumped");

        // Assert
        observation.Context.Should().NotBeNull();
        observation.Context.Should().BeEmpty();
    }

    [Fact]
    public void Action_Factory_WithContext_UsesProvidedContext()
    {
        // Arrange
        var context = new Dictionary<string, object> { ["location"] = "room-A" };

        // Act
        var observation = AgentObservation.Action("agent-1", "moved", context);

        // Assert
        observation.Context.Should().ContainKey("location");
        observation.Context["location"].Should().Be("room-A");
    }

    [Fact]
    public void Statement_Factory_SetsTypeToStatement()
    {
        // Act
        var observation = AgentObservation.Statement("agent-2", "I believe X is true");

        // Assert
        observation.AgentId.Should().Be("agent-2");
        observation.ObservationType.Should().Be("statement");
        observation.Content.Should().Be("I believe X is true");
    }

    [Fact]
    public void Statement_Factory_NullContext_DefaultsToEmptyDictionary()
    {
        // Act
        var observation = AgentObservation.Statement("agent-2", "hello");

        // Assert
        observation.Context.Should().NotBeNull();
        observation.Context.Should().BeEmpty();
    }

    [Fact]
    public void Statement_Factory_WithContext_UsesProvidedContext()
    {
        // Arrange
        var context = new Dictionary<string, object> { ["confidence"] = 0.9 };

        // Act
        var observation = AgentObservation.Statement("agent-2", "claim", context);

        // Assert
        observation.Context.Should().ContainKey("confidence");
    }

    [Fact]
    public void StateChange_Factory_SetsTypeToStateChange()
    {
        // Act
        var observation = AgentObservation.StateChange("agent-3", "became idle");

        // Assert
        observation.AgentId.Should().Be("agent-3");
        observation.ObservationType.Should().Be("state_change");
        observation.Content.Should().Be("became idle");
    }

    [Fact]
    public void StateChange_Factory_NullContext_DefaultsToEmptyDictionary()
    {
        // Act
        var observation = AgentObservation.StateChange("agent-3", "activated");

        // Assert
        observation.Context.Should().NotBeNull();
        observation.Context.Should().BeEmpty();
    }

    [Fact]
    public void StateChange_Factory_WithContext_UsesProvidedContext()
    {
        // Arrange
        var context = new Dictionary<string, object> { ["previousState"] = "active", ["newState"] = "idle" };

        // Act
        var observation = AgentObservation.StateChange("agent-3", "state changed", context);

        // Assert
        observation.Context.Should().HaveCount(2);
        observation.Context.Should().ContainKey("previousState");
        observation.Context.Should().ContainKey("newState");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var context = new Dictionary<string, object>();
        var timestamp = DateTime.UtcNow;
        var a = new AgentObservation("agent-1", "action", "content", context, timestamp);
        var b = new AgentObservation("agent-1", "action", "content", context, timestamp);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentAgentId_AreNotEqual()
    {
        // Arrange
        var context = new Dictionary<string, object>();
        var timestamp = DateTime.UtcNow;
        var a = new AgentObservation("agent-1", "action", "content", context, timestamp);
        var b = new AgentObservation("agent-2", "action", "content", context, timestamp);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentObservationType_AreNotEqual()
    {
        // Arrange
        var context = new Dictionary<string, object>();
        var timestamp = DateTime.UtcNow;
        var a = new AgentObservation("agent-1", "action", "content", context, timestamp);
        var b = new AgentObservation("agent-1", "statement", "content", context, timestamp);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void AllFactories_SetObservedAtToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var action = AgentObservation.Action("a", "desc");
        var statement = AgentObservation.Statement("a", "desc");
        var stateChange = AgentObservation.StateChange("a", "desc");

        var after = DateTime.UtcNow;

        // Assert
        action.ObservedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        statement.ObservedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        stateChange.ObservedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}

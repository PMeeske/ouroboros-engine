// <copyright file="AgentIdentityStateTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;
using AgentCapability = Ouroboros.Agent.MetaAI.AgentCapability;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class AgentIdentityStateTests
{
    private static AgentPerformance CreatePerformance() =>
        new(0.85, 150.0, 100, 85, 15,
            new Dictionary<string, double> { ["reasoning"] = 0.9 },
            new Dictionary<string, double> { ["cpu"] = 0.6 },
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var name = "TestAgent";
        var capabilities = new List<AgentCapability>();
        var resources = new List<AgentResource>();
        var commitments = new List<AgentCommitment>();
        var performance = CreatePerformance();
        var stateTimestamp = DateTime.UtcNow;
        var metadata = new Dictionary<string, object> { ["version"] = "1.0" };

        // Act
        var state = new AgentIdentityState(
            agentId, name, capabilities, resources, commitments,
            performance, stateTimestamp, metadata);

        // Assert
        state.AgentId.Should().Be(agentId);
        state.Name.Should().Be(name);
        state.Capabilities.Should().BeSameAs(capabilities);
        state.Resources.Should().BeSameAs(resources);
        state.Commitments.Should().BeSameAs(commitments);
        state.Performance.Should().Be(performance);
        state.StateTimestamp.Should().Be(stateTimestamp);
        state.Metadata.Should().BeSameAs(metadata);
    }

    [Fact]
    public void Constructor_WithEmptyCollections_Succeeds()
    {
        var state = new AgentIdentityState(
            Guid.NewGuid(), "Agent",
            new List<AgentCapability>(),
            new List<AgentResource>(),
            new List<AgentCommitment>(),
            CreatePerformance(),
            DateTime.UtcNow,
            new Dictionary<string, object>());

        state.Capabilities.Should().BeEmpty();
        state.Resources.Should().BeEmpty();
        state.Commitments.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var performance = CreatePerformance();
        var timestamp = DateTime.UtcNow;
        var capabilities = new List<AgentCapability>();
        var resources = new List<AgentResource>();
        var commitments = new List<AgentCommitment>();
        var metadata = new Dictionary<string, object>();

        var a = new AgentIdentityState(id, "Agent", capabilities, resources, commitments, performance, timestamp, metadata);
        var b = new AgentIdentityState(id, "Agent", capabilities, resources, commitments, performance, timestamp, metadata);

        a.Should().Be(b);
    }

    [Fact]
    public void With_CanUpdateName()
    {
        var original = new AgentIdentityState(
            Guid.NewGuid(), "OriginalName",
            new List<AgentCapability>(),
            new List<AgentResource>(),
            new List<AgentCommitment>(),
            CreatePerformance(),
            DateTime.UtcNow,
            new Dictionary<string, object>());

        var updated = original with { Name = "UpdatedName" };

        updated.Name.Should().Be("UpdatedName");
        updated.AgentId.Should().Be(original.AgentId);
    }
}

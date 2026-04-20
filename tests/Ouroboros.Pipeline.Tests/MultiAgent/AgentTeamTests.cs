using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentTeamTests
{
    private static AgentIdentity CreateIdentity(string name, AgentRole role) =>
        AgentIdentity.Create(name, role);

    [Fact]
    public void Empty_ReturnsTeamWithZeroCount()
    {
        AgentTeam.Empty.Count.Should().Be(0);
    }

    [Fact]
    public void AddAgent_IncreasesCount()
    {
        // Arrange
        var identity = CreateIdentity("Agent1", AgentRole.Coder);

        // Act
        var team = AgentTeam.Empty.AddAgent(identity);

        // Assert
        team.Count.Should().Be(1);
    }

    [Fact]
    public void AddAgent_WithNullIdentity_ThrowsArgumentNullException()
    {
        Action act = () => AgentTeam.Empty.AddAgent(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("identity");
    }

    [Fact]
    public void RemoveAgent_DecreasesCount()
    {
        // Arrange
        var identity = CreateIdentity("Agent1", AgentRole.Coder);
        var team = AgentTeam.Empty.AddAgent(identity);

        // Act
        var updated = team.RemoveAgent(identity.Id);

        // Assert
        updated.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveAgent_WithNonexistentId_ReturnsSameCount()
    {
        var team = AgentTeam.Empty;
        var updated = team.RemoveAgent(Guid.NewGuid());
        updated.Count.Should().Be(0);
    }

    [Fact]
    public void GetAgent_WhenExists_ReturnsSome()
    {
        // Arrange
        var identity = CreateIdentity("Agent1", AgentRole.Coder);
        var team = AgentTeam.Empty.AddAgent(identity);

        // Act
        var result = team.GetAgent(identity.Id);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Identity.Name.Should().Be("Agent1");
    }

    [Fact]
    public void GetAgent_WhenNotExists_ReturnsNone()
    {
        var result = AgentTeam.Empty.GetAgent(Guid.NewGuid());
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetAvailableAgents_ReturnsOnlyIdleAgents()
    {
        // Arrange
        var agent1 = CreateIdentity("Idle", AgentRole.Coder);
        var agent2 = CreateIdentity("Busy", AgentRole.Analyst);
        var team = AgentTeam.Empty.AddAgent(agent1).AddAgent(agent2);

        // Make agent2 busy
        var busyState = team.GetAgent(agent2.Id).Value!.StartTask(Guid.NewGuid());
        team = team.UpdateAgent(agent2.Id, busyState);

        // Act
        var available = team.GetAvailableAgents();

        // Assert
        available.Should().HaveCount(1);
        available[0].Identity.Name.Should().Be("Idle");
    }

    [Fact]
    public void GetAgentsWithCapability_ReturnsMatchingAgents()
    {
        // Arrange
        var cap = AgentCapability.Create("coding", "Write code");
        var agent1 = CreateIdentity("Coder", AgentRole.Coder).WithCapability(cap);
        var agent2 = CreateIdentity("Analyst", AgentRole.Analyst);
        var team = AgentTeam.Empty.AddAgent(agent1).AddAgent(agent2);

        // Act
        var result = team.GetAgentsWithCapability("coding");

        // Assert
        result.Should().HaveCount(1);
        result[0].Identity.Name.Should().Be("Coder");
    }

    [Fact]
    public void GetAgentsWithCapability_WithNullCapability_ThrowsArgumentNullException()
    {
        Action act = () => AgentTeam.Empty.GetAgentsWithCapability(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("capability");
    }

    [Fact]
    public void GetAgentsByRole_ReturnsMatchingAgents()
    {
        // Arrange
        var coder1 = CreateIdentity("Coder1", AgentRole.Coder);
        var coder2 = CreateIdentity("Coder2", AgentRole.Coder);
        var analyst = CreateIdentity("Analyst", AgentRole.Analyst);
        var team = AgentTeam.Empty.AddAgent(coder1).AddAgent(coder2).AddAgent(analyst);

        // Act
        var result = team.GetAgentsByRole(AgentRole.Coder);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetAllAgents_ReturnsAllAgents()
    {
        // Arrange
        var team = AgentTeam.Empty
            .AddAgent(CreateIdentity("A1", AgentRole.Coder))
            .AddAgent(CreateIdentity("A2", AgentRole.Analyst))
            .AddAgent(CreateIdentity("A3", AgentRole.Reviewer));

        // Act
        var all = team.GetAllAgents();

        // Assert
        all.Should().HaveCount(3);
    }

    [Fact]
    public void UpdateAgent_WithExistingAgent_UpdatesState()
    {
        // Arrange
        var identity = CreateIdentity("Agent", AgentRole.Coder);
        var team = AgentTeam.Empty.AddAgent(identity);
        var newState = team.GetAgent(identity.Id).Value!.WithStatus(AgentStatus.Busy);

        // Act
        var updated = team.UpdateAgent(identity.Id, newState);

        // Assert
        updated.GetAgent(identity.Id).Value!.Status.Should().Be(AgentStatus.Busy);
    }

    [Fact]
    public void UpdateAgent_WithNonexistentAgent_ReturnsSameTeam()
    {
        // Arrange
        var identity = CreateIdentity("Agent", AgentRole.Coder);
        var team = AgentTeam.Empty.AddAgent(identity);
        var newState = AgentState.ForAgent(CreateIdentity("Other", AgentRole.Analyst));

        // Act
        var updated = team.UpdateAgent(Guid.NewGuid(), newState);

        // Assert
        updated.Count.Should().Be(1);
    }

    [Fact]
    public void UpdateAgent_WithNullState_ThrowsArgumentNullException()
    {
        var identity = CreateIdentity("Agent", AgentRole.Coder);
        var team = AgentTeam.Empty.AddAgent(identity);
        Action act = () => team.UpdateAgent(identity.Id, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("newState");
    }

    [Fact]
    public void ImmutabilityPreserved_AddAgentDoesNotModifyOriginal()
    {
        // Arrange
        var original = AgentTeam.Empty;

        // Act
        var modified = original.AddAgent(CreateIdentity("Agent", AgentRole.Coder));

        // Assert
        original.Count.Should().Be(0);
        modified.Count.Should().Be(1);
    }
}

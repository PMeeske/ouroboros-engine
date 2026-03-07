namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class AgentTeamTests
{
    private static AgentIdentity CreateIdentity(string name = "agent1", AgentRole role = AgentRole.Executor)
        => AgentIdentity.Create(name, role);

    [Fact]
    public void Empty_HasZeroCount()
    {
        AgentTeam.Empty.Count.Should().Be(0);
    }

    [Fact]
    public void AddAgent_IncreasesCount()
    {
        var identity = CreateIdentity();
        var team = AgentTeam.Empty.AddAgent(identity);

        team.Count.Should().Be(1);
    }

    [Fact]
    public void AddAgent_ThrowsOnNull()
    {
        var act = () => AgentTeam.Empty.AddAgent(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveAgent_DecreasesCount()
    {
        var identity = CreateIdentity();
        var team = AgentTeam.Empty.AddAgent(identity);
        var removed = team.RemoveAgent(identity.Id);

        removed.Count.Should().Be(0);
    }

    [Fact]
    public void GetAgent_ReturnsAgentWhenExists()
    {
        var identity = CreateIdentity();
        var team = AgentTeam.Empty.AddAgent(identity);

        var agent = team.GetAgent(identity.Id);
        agent.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetAgent_ReturnsNoneWhenNotExists()
    {
        var agent = AgentTeam.Empty.GetAgent(Guid.NewGuid());
        agent.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetAvailableAgents_ReturnsIdleAgents()
    {
        var identity = CreateIdentity();
        var team = AgentTeam.Empty.AddAgent(identity);

        var available = team.GetAvailableAgents();
        available.Should().HaveCount(1);
    }

    [Fact]
    public void GetAgentsWithCapability_FiltersCorrectly()
    {
        var identity = CreateIdentity()
            .WithCapability(AgentCapability.Create("coding", "Writes code", 0.9));
        var team = AgentTeam.Empty.AddAgent(identity);

        var coders = team.GetAgentsWithCapability("coding");
        coders.Should().HaveCount(1);

        var nonExistent = team.GetAgentsWithCapability("flying");
        nonExistent.Should().BeEmpty();
    }

    [Fact]
    public void GetAgentsByRole_FiltersCorrectly()
    {
        var executor = CreateIdentity("w1", AgentRole.Executor);
        var planner = CreateIdentity("c1", AgentRole.Planner);
        var team = AgentTeam.Empty.AddAgent(executor).AddAgent(planner);

        var executors = team.GetAgentsByRole(AgentRole.Executor);
        executors.Should().HaveCount(1);
    }

    [Fact]
    public void GetAllAgents_ReturnsAll()
    {
        var i1 = CreateIdentity("a1");
        var i2 = CreateIdentity("a2");
        var team = AgentTeam.Empty.AddAgent(i1).AddAgent(i2);

        team.GetAllAgents().Should().HaveCount(2);
    }
}

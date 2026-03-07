using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;
using Xunit;

namespace Ouroboros.Tests.Pipeline.MultiAgent.Strategies;

[Trait("Category", "Unit")]
public sealed class RoleBasedStrategyDeepTests
{
    private static readonly DelegationCriteria DefaultCriteria =
        DelegationCriteria.FromGoal(Goal.Atomic("test"));

    private static AgentIdentity CreateAgent(
        string name,
        AgentRole role = AgentRole.Executor,
        params (string Name, double Proficiency)[] capabilities)
    {
        var identity = AgentIdentity.Create(name, role);
        foreach (var (capName, prof) in capabilities)
        {
            identity = identity.WithCapability(AgentCapability.Create(capName, $"{capName} desc", prof));
        }
        return identity;
    }

    private static AgentTeam BuildTeam(params AgentIdentity[] agents)
    {
        var team = AgentTeam.Empty;
        foreach (var a in agents) team = team.AddAgent(a);
        return team;
    }

    [Fact]
    public void SelectAgent_EmptyTeam_NoMatch()
    {
        var sut = new RoleBasedStrategy();

        var result = sut.SelectAgent(DefaultCriteria, AgentTeam.Empty);

        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_NoPreferredRole_DelegatesToCapability()
    {
        var agent = CreateAgent("A", AgentRole.Coder, ("coding", 0.8));
        var team = BuildTeam(agent);
        var sut = new RoleBasedStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_PreferredRoleMatches_SelectsMatchingAgent()
    {
        var coder = CreateAgent("Coder", AgentRole.Coder);
        var analyst = CreateAgent("Analyst", AgentRole.Analyst);
        var team = BuildTeam(coder, analyst);
        var criteria = DefaultCriteria.WithPreferredRole(AgentRole.Coder);
        var sut = new RoleBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(coder.Id);
        result.Reasoning.Should().Contain("matching role");
    }

    [Fact]
    public void SelectAgent_NoRoleMatch_FallsBackToOther()
    {
        var executor = CreateAgent("Exec", AgentRole.Executor);
        var team = BuildTeam(executor);
        var criteria = DefaultCriteria.WithPreferredRole(AgentRole.Coder);
        var sut = new RoleBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeTrue();
        result.Reasoning.Should().Contain("fallback");
    }

    [Fact]
    public void SelectAgent_RoleMatchGetsBonus()
    {
        var coder = CreateAgent("Coder", AgentRole.Coder);
        var executor = CreateAgent("Executor", AgentRole.Executor);
        var team = BuildTeam(coder, executor);
        var criteria = DefaultCriteria.WithPreferredRole(AgentRole.Coder);
        var sut = new RoleBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        // Coder should get the role bonus
        result.SelectedAgentId.Should().Be(coder.Id);
    }

    [Fact]
    public void SelectAgents_ReturnsCorrectCount()
    {
        var team = BuildTeam(
            CreateAgent("A", AgentRole.Coder),
            CreateAgent("B", AgentRole.Coder),
            CreateAgent("C", AgentRole.Analyst));
        var criteria = DefaultCriteria.WithPreferredRole(AgentRole.Coder);
        var sut = new RoleBasedStrategy();

        var results = sut.SelectAgents(criteria, team, 2);

        results.Should().HaveCount(2);
    }

    [Fact]
    public void SelectAgent_WithCapabilities_IncorporatesInScore()
    {
        var skilled = CreateAgent("Skilled", AgentRole.Coder, ("coding", 0.9));
        var unskilled = CreateAgent("Unskilled", AgentRole.Coder);
        var team = BuildTeam(skilled, unskilled);
        var criteria = DefaultCriteria
            .WithPreferredRole(AgentRole.Coder)
            .RequireCapability("coding");
        var sut = new RoleBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.SelectedAgentId.Should().Be(skilled.Id);
    }

    [Fact]
    public void SelectAgent_WithMinProficiency_FiltersLowScorers()
    {
        var low = CreateAgent("Low", AgentRole.Coder);
        var team = BuildTeam(low);
        // No capabilities on agent, default success rate for new agent is 1.0
        // But requiring capability with min proficiency should filter out
        var criteria = DefaultCriteria
            .WithPreferredRole(AgentRole.Coder)
            .RequireCapability("advanced-math")
            .WithMinProficiency(0.9);
        var sut = new RoleBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        // Agent doesn't have the capability so coverage score will be low
        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgents_ZeroCount_Throws()
    {
        var sut = new RoleBasedStrategy();

        var act = () => sut.SelectAgents(DefaultCriteria, AgentTeam.Empty, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SelectAgent_ProvidesAlternatives()
    {
        var team = BuildTeam(
            CreateAgent("A", AgentRole.Coder),
            CreateAgent("B", AgentRole.Coder),
            CreateAgent("C", AgentRole.Coder));
        var criteria = DefaultCriteria.WithPreferredRole(AgentRole.Coder);
        var sut = new RoleBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.Alternatives.Should().NotBeEmpty();
    }

    [Fact]
    public void Name_ReturnsRoleBased()
    {
        new RoleBasedStrategy().Name.Should().Be("RoleBased");
    }

    [Fact]
    public void SelectAgent_PreferAvailableBoosts()
    {
        var agent = CreateAgent("A", AgentRole.Coder);
        var team = BuildTeam(agent);
        var criteria = DefaultCriteria
            .WithPreferredRole(AgentRole.Coder)
            .WithAvailabilityPreference(true);
        var sut = new RoleBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeTrue();
    }
}

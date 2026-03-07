using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;
using Xunit;

namespace Ouroboros.Tests.Pipeline.MultiAgent.Strategies;

[Trait("Category", "Unit")]
public sealed class LoadBalancingStrategyDeepTests
{
    private static readonly DelegationCriteria DefaultCriteria =
        DelegationCriteria.FromGoal(Goal.Atomic("test"));

    private static AgentIdentity CreateAgent(string name, AgentRole role = AgentRole.Executor)
        => AgentIdentity.Create(name, role);

    private static AgentTeam BuildTeam(params AgentIdentity[] agents)
    {
        var team = AgentTeam.Empty;
        foreach (var a in agents) team = team.AddAgent(a);
        return team;
    }

    [Fact]
    public void SelectAgent_SingleAgent_SelectsIt()
    {
        var agent = CreateAgent("Solo");
        var team = BuildTeam(agent);
        var sut = new LoadBalancingStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(agent.Id);
    }

    [Fact]
    public void SelectAgent_EmptyTeam_NoMatch()
    {
        var sut = new LoadBalancingStrategy();

        var result = sut.SelectAgent(DefaultCriteria, AgentTeam.Empty);

        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgents_MultipleAgents_ReturnsRequestedCount()
    {
        var team = BuildTeam(CreateAgent("A"), CreateAgent("B"), CreateAgent("C"));
        var sut = new LoadBalancingStrategy();

        var results = sut.SelectAgents(DefaultCriteria, team, 2);

        results.Should().HaveCount(2);
    }

    [Fact]
    public void SelectAgents_ZeroCount_Throws()
    {
        var sut = new LoadBalancingStrategy();

        var act = () => sut.SelectAgents(DefaultCriteria, AgentTeam.Empty, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SelectAgents_NegativeCount_Throws()
    {
        var sut = new LoadBalancingStrategy();

        var act = () => sut.SelectAgents(DefaultCriteria, AgentTeam.Empty, -5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SelectAgent_NullCriteria_Throws()
    {
        var sut = new LoadBalancingStrategy();

        var act = () => sut.SelectAgent(null!, AgentTeam.Empty);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_NullTeam_Throws()
    {
        var sut = new LoadBalancingStrategy();

        var act = () => sut.SelectAgent(DefaultCriteria, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_ReasoningContainsAgentName()
    {
        var agent = CreateAgent("Worker1");
        var team = BuildTeam(agent);
        var sut = new LoadBalancingStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.Reasoning.Should().Contain("Worker1");
    }

    [Fact]
    public void SelectAgent_ScoreIsBetweenZeroAndOne()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var sut = new LoadBalancingStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.MatchScore.Should().BeGreaterThanOrEqualTo(0.0);
        result.MatchScore.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void SelectAgents_WithPreferAvailableFalse_FallsBackToAll()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var criteria = DefaultCriteria.WithAvailabilityPreference(false);
        var sut = new LoadBalancingStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_ProvidesAlternativesWhenMultipleAgents()
    {
        var team = BuildTeam(CreateAgent("A"), CreateAgent("B"), CreateAgent("C"));
        var sut = new LoadBalancingStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.Alternatives.Should().NotBeEmpty();
    }

    [Fact]
    public void SelectAgents_CountExceedsTeamSize_ReturnsCapped()
    {
        var team = BuildTeam(CreateAgent("A"));
        var sut = new LoadBalancingStrategy();

        var results = sut.SelectAgents(DefaultCriteria, team, 10);

        results.Should().HaveCount(1);
    }

    [Fact]
    public void SelectAgent_ReasoningContainsLoadScore()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var sut = new LoadBalancingStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.Reasoning.Should().Contain("Load score:");
    }
}

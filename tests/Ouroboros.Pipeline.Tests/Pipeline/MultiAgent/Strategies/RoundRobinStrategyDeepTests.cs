using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;
using Xunit;

namespace Ouroboros.Tests.Pipeline.MultiAgent.Strategies;

[Trait("Category", "Unit")]
public sealed class RoundRobinStrategyDeepTests
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
    public void SelectAgent_SingleAgent_ReturnsMatch()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var sut = new RoundRobinStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(agent.Id);
        result.MatchScore.Should().Be(1.0);
    }

    [Fact]
    public void SelectAgents_ReturnsRequestedCount()
    {
        var team = BuildTeam(CreateAgent("A"), CreateAgent("B"), CreateAgent("C"));
        var sut = new RoundRobinStrategy();

        var results = sut.SelectAgents(DefaultCriteria, team, 2);

        results.Should().HaveCount(2);
    }

    [Fact]
    public void SelectAgents_ScoresDecreaseByPosition()
    {
        var team = BuildTeam(CreateAgent("A"), CreateAgent("B"), CreateAgent("C"));
        var sut = new RoundRobinStrategy();

        var results = sut.SelectAgents(DefaultCriteria, team, 3);

        results[0].MatchScore.Should().BeGreaterThan(results[1].MatchScore);
        results[1].MatchScore.Should().BeGreaterThan(results[2].MatchScore);
    }

    [Fact]
    public void SelectAgents_AdvancesIndexCorrectly()
    {
        var a = CreateAgent("A");
        var b = CreateAgent("B");
        var c = CreateAgent("C");
        var team = BuildTeam(a, b, c);
        var sut = new RoundRobinStrategy();

        // First call selects 2 agents starting at index 0
        var first = sut.SelectAgents(DefaultCriteria, team, 2);
        // Second call should start at index 2
        var second = sut.SelectAgent(DefaultCriteria, team);

        // The agents selected in the first and second calls should not overlap in primary
        first.Select(r => r.SelectedAgentId).Should().NotContain(second.SelectedAgentId);
    }

    [Fact]
    public void SelectAgents_WrapsAroundAfterEndOfList()
    {
        var a = CreateAgent("A");
        var b = CreateAgent("B");
        var team = BuildTeam(a, b);
        var sut = new RoundRobinStrategy();

        // Exhaust the list
        sut.SelectAgents(DefaultCriteria, team, 2);
        // Next selection should wrap
        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_ProvidesAlternatives()
    {
        var team = BuildTeam(CreateAgent("A"), CreateAgent("B"), CreateAgent("C"));
        var sut = new RoundRobinStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.Alternatives.Should().NotBeEmpty();
        result.Alternatives.Should().HaveCountLessThanOrEqualTo(3);
    }

    [Fact]
    public void SelectAgent_WithPreferAvailableFalse_UsesAllAgents()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var criteria = DefaultCriteria.WithAvailabilityPreference(false);
        var sut = new RoundRobinStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgents_NegativeCount_Throws()
    {
        var sut = new RoundRobinStrategy();

        var act = () => sut.SelectAgents(DefaultCriteria, AgentTeam.Empty, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SelectAgent_ReasoningContainsAgentName()
    {
        var agent = CreateAgent("MySpecialAgent");
        var team = BuildTeam(agent);
        var sut = new RoundRobinStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.Reasoning.Should().Contain("MySpecialAgent");
    }

    [Fact]
    public void Reset_AfterMultipleSelections_RestartsFromZero()
    {
        var a = CreateAgent("A");
        var b = CreateAgent("B");
        var team = BuildTeam(a, b);
        var sut = new RoundRobinStrategy();

        var first = sut.SelectAgent(DefaultCriteria, team);
        sut.SelectAgent(DefaultCriteria, team);
        sut.SelectAgent(DefaultCriteria, team);
        sut.Reset();
        var afterReset = sut.SelectAgent(DefaultCriteria, team);

        afterReset.SelectedAgentId.Should().Be(first.SelectedAgentId);
    }

    [Fact]
    public void SelectAgents_EmptyTeam_ReturnsEmptyList()
    {
        var sut = new RoundRobinStrategy();

        var results = sut.SelectAgents(DefaultCriteria, AgentTeam.Empty, 3);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Name_ReturnsRoundRobin()
    {
        new RoundRobinStrategy().Name.Should().Be("RoundRobin");
    }
}

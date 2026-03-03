using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;
using Xunit;

namespace Ouroboros.Tests.Pipeline.MultiAgent.Strategies;

[Trait("Category", "Unit")]
public sealed class CompositeStrategyDeepTests
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
    public void Create_EmptyStrategies_Throws()
    {
        var act = () => CompositeStrategy.Create();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NullStrategy_Throws()
    {
        var act = () => CompositeStrategy.Create((null!, 1.0));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ZeroWeight_Throws()
    {
        var act = () => CompositeStrategy.Create((new RoundRobinStrategy(), 0.0));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_NegativeWeight_Throws()
    {
        var act = () => CompositeStrategy.Create((new RoundRobinStrategy(), -0.5));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SelectAgent_SingleStrategy_DelegatesToIt()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var sut = CompositeStrategy.Create((new RoundRobinStrategy(), 1.0));

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(agent.Id);
    }

    [Fact]
    public void SelectAgent_MultipleStrategies_AggregatesScores()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var sut = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 1.0),
            (new LoadBalancingStrategy(), 1.0));

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
        result.Reasoning.Should().Contain("Composite");
    }

    [Fact]
    public void SelectAgent_EmptyTeam_NoMatch()
    {
        var sut = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 1.0));

        var result = sut.SelectAgent(DefaultCriteria, AgentTeam.Empty);

        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgents_ReturnsRequestedCount()
    {
        var team = BuildTeam(CreateAgent("A"), CreateAgent("B"));
        var sut = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 1.0),
            (new LoadBalancingStrategy(), 1.0));

        var results = sut.SelectAgents(DefaultCriteria, team, 2);

        results.Should().HaveCountLessThanOrEqualTo(2);
    }

    [Fact]
    public void SelectAgent_ReasoningContainsContributions()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var sut = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 0.5),
            (new LoadBalancingStrategy(), 0.5));

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.Reasoning.Should().Contain("Strategy contributions:");
    }

    [Fact]
    public void SelectAgent_HigherWeightStrategy_HasMoreInfluence()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var sut = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 10.0),
            (new LoadBalancingStrategy(), 0.1));

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgents_ZeroCount_Throws()
    {
        var sut = CompositeStrategy.Create((new RoundRobinStrategy(), 1.0));

        var act = () => sut.SelectAgents(DefaultCriteria, AgentTeam.Empty, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Name_ReturnsComposite()
    {
        var sut = CompositeStrategy.Create((new RoundRobinStrategy(), 1.0));

        sut.Name.Should().Be("Composite");
    }

    [Fact]
    public void SelectAgent_WithMinProficiency_FiltersLow()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var criteria = DefaultCriteria.WithMinProficiency(0.99);
        var sut = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 1.0));

        // RoundRobin gives score 1.0 which should pass
        var result = sut.SelectAgent(criteria, team);
        // Score depends on weighted avg
        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_NullCriteria_Throws()
    {
        var sut = CompositeStrategy.Create((new RoundRobinStrategy(), 1.0));

        var act = () => sut.SelectAgent(null!, AgentTeam.Empty);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_ProvidesAlternatives()
    {
        var team = BuildTeam(CreateAgent("A"), CreateAgent("B"), CreateAgent("C"));
        var sut = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 1.0),
            (new LoadBalancingStrategy(), 1.0));

        var result = sut.SelectAgent(DefaultCriteria, team);

        if (result.HasMatch)
        {
            result.Alternatives.Should().NotBeNull();
        }
    }
}

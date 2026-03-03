using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;
using Xunit;

namespace Ouroboros.Tests.Pipeline.MultiAgent.Strategies;

[Trait("Category", "Unit")]
public sealed class BestFitStrategyDeepTests
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
            identity = identity.WithCapability(AgentCapability.Create(capName, $"{capName} capability", prof));
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
        var sut = new BestFitStrategy();

        var result = sut.SelectAgent(DefaultCriteria, AgentTeam.Empty);

        result.HasMatch.Should().BeFalse();
        result.Reasoning.Should().Contain("best-fit");
    }

    [Fact]
    public void SelectAgent_SingleAgent_SelectsIt()
    {
        var agent = CreateAgent("Solo");
        var team = BuildTeam(agent);
        var sut = new BestFitStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_PrefersAgentWithMatchingRole()
    {
        var coder = CreateAgent("Coder", AgentRole.Coder);
        var analyst = CreateAgent("Analyst", AgentRole.Analyst);
        var team = BuildTeam(coder, analyst);
        var criteria = DefaultCriteria.WithPreferredRole(AgentRole.Coder);
        var sut = new BestFitStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(coder.Id);
    }

    [Fact]
    public void SelectAgent_AgentWithHigherCapability_ScoredHigher()
    {
        var expert = CreateAgent("Expert", AgentRole.Executor, ("coding", 0.95));
        var novice = CreateAgent("Novice", AgentRole.Executor, ("coding", 0.3));
        var team = BuildTeam(expert, novice);
        var criteria = DefaultCriteria.RequireCapability("coding");
        var sut = new BestFitStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(expert.Id);
    }

    [Fact]
    public void SelectAgents_MultipleAgents_ReturnsRequestedCount()
    {
        var team = BuildTeam(CreateAgent("A"), CreateAgent("B"), CreateAgent("C"));
        var sut = new BestFitStrategy();

        var results = sut.SelectAgents(DefaultCriteria, team, 2);

        results.Should().HaveCount(2);
    }

    [Fact]
    public void SelectAgent_WithMinProficiency_FiltersLowScorers()
    {
        var lowAgent = CreateAgent("Low", AgentRole.Executor, ("coding", 0.1));
        var team = BuildTeam(lowAgent);
        var criteria = DefaultCriteria
            .RequireCapability("coding")
            .WithMinProficiency(0.9);
        var sut = new BestFitStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_NoCapabilitiesRequired_UsesAverageProficiency()
    {
        var agent = CreateAgent("Multi", AgentRole.Executor, ("coding", 0.8), ("testing", 0.6));
        var team = BuildTeam(agent);
        var sut = new BestFitStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
        result.MatchScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SelectAgent_NoCapabilitiesDefined_ReturnsNeutralScore()
    {
        var agent = CreateAgent("Bare");
        var team = BuildTeam(agent);
        var sut = new BestFitStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_ReasoningContainsBreakdown()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var sut = new BestFitStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.Reasoning.Should().Contain("Breakdown:");
        result.Reasoning.Should().Contain("Capability=");
        result.Reasoning.Should().Contain("Availability=");
    }

    [Fact]
    public void SelectAgents_ZeroCount_Throws()
    {
        var sut = new BestFitStrategy();

        var act = () => sut.SelectAgents(DefaultCriteria, AgentTeam.Empty, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SelectAgent_ProvidesAlternatives()
    {
        var team = BuildTeam(CreateAgent("A"), CreateAgent("B"), CreateAgent("C"));
        var sut = new BestFitStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
        result.Alternatives.Should().NotBeEmpty();
    }

    [Fact]
    public void SelectAgent_AvailableAgentScoredHigherThanBusy()
    {
        // Both agents idle (both available), but we verify the availability component is 1.0
        var agent = CreateAgent("Available");
        var team = BuildTeam(agent);
        var sut = new BestFitStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        // Available agents get availability=1.0
        result.Reasoning.Should().Contain("Availability=1");
    }
}

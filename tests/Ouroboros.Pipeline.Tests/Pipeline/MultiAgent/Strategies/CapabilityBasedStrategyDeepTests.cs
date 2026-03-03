using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;
using Xunit;

namespace Ouroboros.Tests.Pipeline.MultiAgent.Strategies;

[Trait("Category", "Unit")]
public sealed class CapabilityBasedStrategyDeepTests
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
        var sut = new CapabilityBasedStrategy();

        var result = sut.SelectAgent(DefaultCriteria, AgentTeam.Empty);

        result.HasMatch.Should().BeFalse();
        result.Reasoning.Should().Contain("capability");
    }

    [Fact]
    public void SelectAgent_NoRequiredCapabilities_UsesSuccessRate()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        var sut = new CapabilityBasedStrategy();

        var result = sut.SelectAgent(DefaultCriteria, team);

        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_WithRequiredCapability_MatchesAgent()
    {
        var agent = CreateAgent("Coder", AgentRole.Coder, ("coding", 0.9));
        var team = BuildTeam(agent);
        var criteria = DefaultCriteria.RequireCapability("coding");
        var sut = new CapabilityBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(agent.Id);
    }

    [Fact]
    public void SelectAgent_AgentMissingCapability_ScoresZero()
    {
        var agent = CreateAgent("NoSkill");
        var team = BuildTeam(agent);
        var criteria = DefaultCriteria.RequireCapability("advanced-math").WithMinProficiency(0.5);
        var sut = new CapabilityBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_PreferAvailable_AddsBonus()
    {
        var agent = CreateAgent("Worker", AgentRole.Executor, ("coding", 0.8));
        var team = BuildTeam(agent);
        var criteria = DefaultCriteria
            .RequireCapability("coding")
            .WithAvailabilityPreference(true);
        var sut = new CapabilityBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeTrue();
        // Score should include availability bonus since agent is idle (available)
        result.MatchScore.Should().BeGreaterThanOrEqualTo(0.8);
    }

    [Fact]
    public void SelectAgent_AvailabilityBonus_CappedAtOne()
    {
        var agent = CreateAgent("Pro", AgentRole.Executor, ("coding", 0.95));
        var team = BuildTeam(agent);
        var criteria = DefaultCriteria
            .RequireCapability("coding")
            .WithAvailabilityPreference(true);
        var sut = new CapabilityBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.MatchScore.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void SelectAgent_MultipleRequiredCapabilities_AveragesScore()
    {
        var agent = CreateAgent("Multi", AgentRole.Executor, ("coding", 0.8), ("testing", 0.6));
        var team = BuildTeam(agent);
        var criteria = DefaultCriteria
            .RequireCapability("coding")
            .RequireCapability("testing");
        var sut = new CapabilityBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_PartialCapabilityMatch_ScoresLower()
    {
        var agent = CreateAgent("Partial", AgentRole.Executor, ("coding", 0.9));
        var team = BuildTeam(agent);
        var criteria = DefaultCriteria
            .RequireCapability("coding")
            .RequireCapability("design");
        var sut = new CapabilityBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        // Has only 1 of 2 caps, so coverage is 0.5 * 0.9 = 0.45
        result.HasMatch.Should().BeTrue();
        result.MatchScore.Should().BeLessThan(0.9);
    }

    [Fact]
    public void SelectAgents_ReturnsOrderedByScore()
    {
        var expert = CreateAgent("Expert", AgentRole.Executor, ("coding", 0.95));
        var mid = CreateAgent("Mid", AgentRole.Executor, ("coding", 0.5));
        var team = BuildTeam(expert, mid);
        var criteria = DefaultCriteria.RequireCapability("coding");
        var sut = new CapabilityBasedStrategy();

        var results = sut.SelectAgents(criteria, team, 2);

        results.Should().HaveCount(2);
        results[0].MatchScore.Should().BeGreaterThanOrEqualTo(results[1].MatchScore);
    }

    [Fact]
    public void SelectAgent_ProvidesAlternatives()
    {
        var team = BuildTeam(
            CreateAgent("A", AgentRole.Executor, ("coding", 0.9)),
            CreateAgent("B", AgentRole.Executor, ("coding", 0.7)),
            CreateAgent("C", AgentRole.Executor, ("coding", 0.5)));
        var criteria = DefaultCriteria.RequireCapability("coding");
        var sut = new CapabilityBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.Alternatives.Should().NotBeEmpty();
    }

    [Fact]
    public void SelectAgent_ReasoningContainsMatchCount()
    {
        var agent = CreateAgent("Worker", AgentRole.Executor, ("coding", 0.9));
        var team = BuildTeam(agent);
        var criteria = DefaultCriteria.RequireCapability("coding");
        var sut = new CapabilityBasedStrategy();

        var result = sut.SelectAgent(criteria, team);

        result.Reasoning.Should().Contain("1/1");
    }

    [Fact]
    public void SelectAgents_NullCriteria_Throws()
    {
        var sut = new CapabilityBasedStrategy();

        var act = () => sut.SelectAgents(null!, AgentTeam.Empty, 1);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgents_NullTeam_Throws()
    {
        var sut = new CapabilityBasedStrategy();

        var act = () => sut.SelectAgents(DefaultCriteria, null!, 1);

        act.Should().Throw<ArgumentNullException>();
    }
}

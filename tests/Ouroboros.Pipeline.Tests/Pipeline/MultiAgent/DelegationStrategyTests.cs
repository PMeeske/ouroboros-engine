// <copyright file="DelegationStrategyTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using FluentAssertions;
using Xunit;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public sealed class DelegationStrategyTests
{
    private static AgentIdentity CreateIdentity(
        string name = "Agent",
        AgentRole role = AgentRole.Worker,
        params (string Name, double Proficiency)[] capabilities)
    {
        var caps = capabilities.Length > 0
            ? capabilities.Select(c => new AgentCapability(c.Name, c.Proficiency)).ToImmutableList()
            : ImmutableList<AgentCapability>.Empty;

        return new AgentIdentity(Guid.NewGuid(), name, role, caps);
    }

    private static AgentTeam CreateTeamWithAgents(params AgentIdentity[] identities)
    {
        var team = AgentTeam.Empty;
        foreach (var id in identities)
        {
            team = team.AddAgent(id);
        }
        return team;
    }

    // ====================== RoundRobinStrategy ======================

    [Fact]
    public void RoundRobin_Name_IsCorrect()
    {
        new RoundRobinStrategy().Name.Should().Be("RoundRobin");
    }

    [Fact]
    public void RoundRobin_NullCriteria_Throws()
    {
        var strategy = new RoundRobinStrategy();
        Action act = () => strategy.SelectAgent(null!, AgentTeam.Empty);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RoundRobin_NullTeam_Throws()
    {
        var strategy = new RoundRobinStrategy();
        Action act = () => strategy.SelectAgent(DelegationCriteria.Default, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RoundRobin_EmptyTeam_NoMatch()
    {
        var strategy = new RoundRobinStrategy();
        var result = strategy.SelectAgent(DelegationCriteria.Default, AgentTeam.Empty);

        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void RoundRobin_CyclesThroughAgents()
    {
        var a1 = CreateIdentity("Alpha");
        var a2 = CreateIdentity("Beta");
        var team = CreateTeamWithAgents(a1, a2);

        var strategy = new RoundRobinStrategy();
        var criteria = DelegationCriteria.Default;

        var r1 = strategy.SelectAgent(criteria, team);
        var r2 = strategy.SelectAgent(criteria, team);
        var r3 = strategy.SelectAgent(criteria, team);

        r1.SelectedAgentId.Should().NotBe(r2.SelectedAgentId);
        r3.SelectedAgentId.Should().Be(r1.SelectedAgentId);
    }

    [Fact]
    public void RoundRobin_Reset_RestartsFromBeginning()
    {
        var a1 = CreateIdentity("Alpha");
        var a2 = CreateIdentity("Beta");
        var team = CreateTeamWithAgents(a1, a2);

        var strategy = new RoundRobinStrategy();
        var criteria = DelegationCriteria.Default;

        var first = strategy.SelectAgent(criteria, team);
        strategy.SelectAgent(criteria, team); // advance
        strategy.Reset();
        var afterReset = strategy.SelectAgent(criteria, team);

        afterReset.SelectedAgentId.Should().Be(first.SelectedAgentId);
    }

    [Fact]
    public void RoundRobin_SelectAgents_CountExceedsTeam_ReturnsCapped()
    {
        var team = CreateTeamWithAgents(CreateIdentity("A"));
        var strategy = new RoundRobinStrategy();

        var results = strategy.SelectAgents(DelegationCriteria.Default, team, 5);

        results.Should().HaveCount(1);
    }

    [Fact]
    public void RoundRobin_SelectAgents_ZeroCount_Throws()
    {
        var team = CreateTeamWithAgents(CreateIdentity("A"));
        var strategy = new RoundRobinStrategy();

        Action act = () => strategy.SelectAgents(DelegationCriteria.Default, team, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ====================== LoadBalancingStrategy ======================

    [Fact]
    public void LoadBalancing_Name_IsCorrect()
    {
        new LoadBalancingStrategy().Name.Should().Be("LoadBalancing");
    }

    [Fact]
    public void LoadBalancing_EmptyTeam_NoMatch()
    {
        var strategy = new LoadBalancingStrategy();
        var result = strategy.SelectAgent(DelegationCriteria.Default, AgentTeam.Empty);

        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void LoadBalancing_SingleAgent_SelectsIt()
    {
        var agent = CreateIdentity("Solo");
        var team = CreateTeamWithAgents(agent);

        var strategy = new LoadBalancingStrategy();
        var result = strategy.SelectAgent(DelegationCriteria.Default, team);

        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(agent.Id);
    }

    [Fact]
    public void LoadBalancing_SelectAgents_ZeroCount_Throws()
    {
        var strategy = new LoadBalancingStrategy();
        Action act = () => strategy.SelectAgents(DelegationCriteria.Default, AgentTeam.Empty, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ====================== BestFitStrategy ======================

    [Fact]
    public void BestFit_Name_IsCorrect()
    {
        new BestFitStrategy().Name.Should().Be("BestFit");
    }

    [Fact]
    public void BestFit_EmptyTeam_NoMatch()
    {
        var strategy = new BestFitStrategy();
        var result = strategy.SelectAgent(DelegationCriteria.Default, AgentTeam.Empty);

        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void BestFit_SingleAgent_SelectsIt()
    {
        var agent = CreateIdentity("Solo");
        var team = CreateTeamWithAgents(agent);

        var strategy = new BestFitStrategy();
        var result = strategy.SelectAgent(DelegationCriteria.Default, team);

        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void BestFit_PrefersAvailableAgent()
    {
        var agent = CreateIdentity("Available");
        var team = CreateTeamWithAgents(agent);

        var strategy = new BestFitStrategy();
        var result = strategy.SelectAgent(DelegationCriteria.Default, team);

        result.HasMatch.Should().BeTrue();
        result.MatchScore.Should().BeGreaterThan(0);
    }

    // ====================== CapabilityBasedStrategy ======================

    [Fact]
    public void CapabilityBased_Name_IsCorrect()
    {
        new CapabilityBasedStrategy().Name.Should().Be("CapabilityBased");
    }

    [Fact]
    public void CapabilityBased_EmptyTeam_NoMatch()
    {
        var strategy = new CapabilityBasedStrategy();
        var result = strategy.SelectAgent(DelegationCriteria.Default, AgentTeam.Empty);

        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void CapabilityBased_AgentWithCapabilities_Scores()
    {
        var agent = CreateIdentity("Skilled", AgentRole.Worker, ("coding", 0.9));
        var team = CreateTeamWithAgents(agent);

        var criteria = DelegationCriteria.Default;
        var strategy = new CapabilityBasedStrategy();
        var result = strategy.SelectAgent(criteria, team);

        result.HasMatch.Should().BeTrue();
    }

    // ====================== RoleBasedStrategy ======================

    [Fact]
    public void RoleBased_Name_IsCorrect()
    {
        new RoleBasedStrategy().Name.Should().Be("RoleBased");
    }

    [Fact]
    public void RoleBased_NoPreferredRole_FallsBackToCapability()
    {
        var agent = CreateIdentity("Agent");
        var team = CreateTeamWithAgents(agent);

        var strategy = new RoleBasedStrategy();
        var result = strategy.SelectAgent(DelegationCriteria.Default, team);

        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void RoleBased_EmptyTeam_NoMatch()
    {
        var strategy = new RoleBasedStrategy();
        var result = strategy.SelectAgent(DelegationCriteria.Default, AgentTeam.Empty);

        result.HasMatch.Should().BeFalse();
    }

    // ====================== CompositeStrategy ======================

    [Fact]
    public void Composite_NullStrategies_Throws()
    {
        Action act = () => CompositeStrategy.Create(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Composite_EmptyStrategies_Throws()
    {
        Action act = () => CompositeStrategy.Create();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Composite_ZeroWeight_Throws()
    {
        Action act = () => CompositeStrategy.Create(
            (new RoundRobinStrategy(), 0.0));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Composite_NegativeWeight_Throws()
    {
        Action act = () => CompositeStrategy.Create(
            (new RoundRobinStrategy(), -1.0));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Composite_NullStrategyInArray_Throws()
    {
        Action act = () => CompositeStrategy.Create(
            (null!, 1.0));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Composite_Name_IsCorrect()
    {
        var composite = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 1.0));
        composite.Name.Should().Be("Composite");
    }

    [Fact]
    public void Composite_EmptyTeam_ReturnsEmpty()
    {
        var composite = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 1.0));
        var results = composite.SelectAgents(DelegationCriteria.Default, AgentTeam.Empty, 1);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Composite_WithAgent_SelectsIt()
    {
        var agent = CreateIdentity("Agent");
        var team = CreateTeamWithAgents(agent);

        var composite = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 1.0),
            (new LoadBalancingStrategy(), 1.0));

        var result = composite.SelectAgent(DelegationCriteria.Default, team);
        result.HasMatch.Should().BeTrue();
    }

    // ====================== DelegationStrategyFactory ======================

    [Fact]
    public void Factory_ByCapability_ReturnsCapabilityBased()
    {
        var strategy = DelegationStrategyFactory.ByCapability();
        strategy.Name.Should().Be("CapabilityBased");
    }

    [Fact]
    public void Factory_ByRole_ReturnsRoleBased()
    {
        var strategy = DelegationStrategyFactory.ByRole();
        strategy.Name.Should().Be("RoleBased");
    }

    [Fact]
    public void Factory_ByLoad_ReturnsLoadBalancing()
    {
        var strategy = DelegationStrategyFactory.ByLoad();
        strategy.Name.Should().Be("LoadBalancing");
    }

    [Fact]
    public void Factory_RoundRobin_ReturnsRoundRobin()
    {
        var strategy = DelegationStrategyFactory.RoundRobin();
        strategy.Name.Should().Be("RoundRobin");
    }

    [Fact]
    public void Factory_BestFit_ReturnsBestFit()
    {
        var strategy = DelegationStrategyFactory.BestFit();
        strategy.Name.Should().Be("BestFit");
    }

    [Fact]
    public void Factory_Composite_ReturnsComposite()
    {
        var strategy = DelegationStrategyFactory.Composite(
            (new RoundRobinStrategy(), 1.0));
        strategy.Name.Should().Be("Composite");
    }

    [Fact]
    public void Factory_Balanced_ReturnsComposite()
    {
        var strategy = DelegationStrategyFactory.Balanced();
        strategy.Name.Should().Be("Composite");
    }
}

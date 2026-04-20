using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class RoundRobinStrategyTests
{
    private readonly RoundRobinStrategy _strategy = new();

    [Fact]
    public void Name_ReturnsRoundRobin()
    {
        _strategy.Name.Should().Be("RoundRobin");
    }

    [Fact]
    public void SelectAgent_WithEmptyTeam_ReturnsNoMatch()
    {
        var criteria = StrategyTestHelpers.CreateCriteria();
        var result = _strategy.SelectAgent(criteria, AgentTeam.Empty);
        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_CyclesThroughAgents()
    {
        // Arrange
        var agent1 = StrategyTestHelpers.CreateIdentity("Agent1", AgentRole.Coder);
        var agent2 = StrategyTestHelpers.CreateIdentity("Agent2", AgentRole.Analyst);
        var team = StrategyTestHelpers.CreateTeamWithAgents(agent1, agent2);
        var criteria = StrategyTestHelpers.CreateCriteria();

        // Act - select twice to observe rotation
        var result1 = _strategy.SelectAgent(criteria, team);
        var result2 = _strategy.SelectAgent(criteria, team);

        // Assert
        result1.HasMatch.Should().BeTrue();
        result2.HasMatch.Should().BeTrue();
        result1.SelectedAgentId.Should().NotBe(result2.SelectedAgentId);
    }

    [Fact]
    public void SelectAgent_WrapsAroundAfterFullCycle()
    {
        // Arrange
        var agent1 = StrategyTestHelpers.CreateIdentity("Agent1", AgentRole.Coder);
        var agent2 = StrategyTestHelpers.CreateIdentity("Agent2", AgentRole.Analyst);
        var team = StrategyTestHelpers.CreateTeamWithAgents(agent1, agent2);
        var criteria = StrategyTestHelpers.CreateCriteria();

        // Act
        var r1 = _strategy.SelectAgent(criteria, team);
        var r2 = _strategy.SelectAgent(criteria, team);
        var r3 = _strategy.SelectAgent(criteria, team);

        // Assert - r3 should be same as r1 (wrapped around)
        r3.SelectedAgentId.Should().Be(r1.SelectedAgentId);
    }

    [Fact]
    public void SelectAgents_WithNullCriteria_ThrowsArgumentNullException()
    {
        Action act = () => _strategy.SelectAgents(null!, AgentTeam.Empty, 1);
        act.Should().Throw<ArgumentNullException>().WithParameterName("criteria");
    }

    [Fact]
    public void SelectAgents_WithNullTeam_ThrowsArgumentNullException()
    {
        var criteria = StrategyTestHelpers.CreateCriteria();
        Action act = () => _strategy.SelectAgents(criteria, null!, 1);
        act.Should().Throw<ArgumentNullException>().WithParameterName("team");
    }

    [Fact]
    public void SelectAgents_WithZeroCount_ThrowsArgumentOutOfRangeException()
    {
        var criteria = StrategyTestHelpers.CreateCriteria();
        Action act = () => _strategy.SelectAgents(criteria, AgentTeam.Empty, 0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("count");
    }

    [Fact]
    public void SelectAgents_ReturnsRequestedCount()
    {
        // Arrange
        var team = StrategyTestHelpers.CreateTeamWithAgents(
            StrategyTestHelpers.CreateIdentity("A1", AgentRole.Coder),
            StrategyTestHelpers.CreateIdentity("A2", AgentRole.Analyst),
            StrategyTestHelpers.CreateIdentity("A3", AgentRole.Reviewer));
        var criteria = StrategyTestHelpers.CreateCriteria();

        // Act
        var results = _strategy.SelectAgents(criteria, team, 2);

        // Assert
        results.Should().HaveCount(2);
        results[0].MatchScore.Should().BeGreaterThan(results[1].MatchScore);
    }

    [Fact]
    public void SelectAgents_FirstResultHasAlternatives()
    {
        // Arrange
        var team = StrategyTestHelpers.CreateTeamWithAgents(
            StrategyTestHelpers.CreateIdentity("A1", AgentRole.Coder),
            StrategyTestHelpers.CreateIdentity("A2", AgentRole.Analyst));
        var criteria = StrategyTestHelpers.CreateCriteria();

        // Act
        var results = _strategy.SelectAgents(criteria, team, 1);

        // Assert
        results[0].Alternatives.Should().NotBeEmpty();
    }

    [Fact]
    public void Reset_ResetsIndex()
    {
        // Arrange
        var agent1 = StrategyTestHelpers.CreateIdentity("Agent1", AgentRole.Coder);
        var agent2 = StrategyTestHelpers.CreateIdentity("Agent2", AgentRole.Analyst);
        var team = StrategyTestHelpers.CreateTeamWithAgents(agent1, agent2);
        var criteria = StrategyTestHelpers.CreateCriteria();

        var firstResult = _strategy.SelectAgent(criteria, team);

        // Act
        _strategy.Reset();
        var afterReset = _strategy.SelectAgent(criteria, team);

        // Assert
        afterReset.SelectedAgentId.Should().Be(firstResult.SelectedAgentId);
    }
}

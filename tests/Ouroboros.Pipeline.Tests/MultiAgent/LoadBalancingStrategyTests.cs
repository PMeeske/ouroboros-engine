using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class LoadBalancingStrategyTests
{
    private readonly LoadBalancingStrategy _strategy = new();

    [Fact]
    public void Name_ReturnsLoadBalancing()
    {
        _strategy.Name.Should().Be("LoadBalancing");
    }

    [Fact]
    public void SelectAgent_WithEmptyTeam_ReturnsNoMatch()
    {
        var criteria = StrategyTestHelpers.CreateCriteria();
        var result = _strategy.SelectAgent(criteria, AgentTeam.Empty);
        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_PrefersLeastBusyAgent()
    {
        // Arrange
        var fresh = StrategyTestHelpers.CreateIdentity("Fresh", AgentRole.Coder);
        var experienced = StrategyTestHelpers.CreateIdentity("Experienced", AgentRole.Coder);
        var team = StrategyTestHelpers.CreateTeamWithAgents(fresh, experienced);

        // Make experienced agent have completed tasks (higher load)
        var expState = team.GetAgent(experienced.Id).Value!;
        expState = expState.StartTask(Guid.NewGuid()).CompleteTask();
        expState = expState.StartTask(Guid.NewGuid()).CompleteTask();
        expState = expState.StartTask(Guid.NewGuid()).CompleteTask();
        team = team.UpdateAgent(experienced.Id, expState);

        var criteria = StrategyTestHelpers.CreateCriteria();

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert - fresh agent should be preferred (less load)
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(fresh.Id);
    }

    [Fact]
    public void SelectAgent_FallsBackToAllAgentsWhenNoneAvailable()
    {
        // Arrange
        var agent = StrategyTestHelpers.CreateIdentity("Busy", AgentRole.Coder);
        var team = StrategyTestHelpers.CreateTeamWithAgents(agent);

        // Make agent busy
        var busyState = team.GetAgent(agent.Id).Value!.StartTask(Guid.NewGuid());
        team = team.UpdateAgent(agent.Id, busyState);

        var criteria = StrategyTestHelpers.CreateCriteria();

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert - should still find the agent even though busy
        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_WhenNotPreferAvailable_ConsidersAllAgents()
    {
        // Arrange
        var agent = StrategyTestHelpers.CreateIdentity("Agent", AgentRole.Coder);
        var team = StrategyTestHelpers.CreateTeamWithAgents(agent);
        var busyState = team.GetAgent(agent.Id).Value!.StartTask(Guid.NewGuid());
        team = team.UpdateAgent(agent.Id, busyState);

        var criteria = StrategyTestHelpers.CreateCriteria().WithAvailabilityPreference(false);

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_ReasoningIncludesLoadInfo()
    {
        // Arrange
        var agent = StrategyTestHelpers.CreateIdentity("Agent", AgentRole.Coder);
        var team = StrategyTestHelpers.CreateTeamWithAgents(agent);
        var criteria = StrategyTestHelpers.CreateCriteria();

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.Reasoning.Should().Contain("Load score:");
    }

    [Fact]
    public void SelectAgents_WithNullCriteria_ThrowsArgumentNullException()
    {
        Action act = () => _strategy.SelectAgents(null!, AgentTeam.Empty, 1);
        act.Should().Throw<ArgumentNullException>().WithParameterName("criteria");
    }

    [Fact]
    public void SelectAgents_WithZeroCount_ThrowsArgumentOutOfRangeException()
    {
        var criteria = StrategyTestHelpers.CreateCriteria();
        Action act = () => _strategy.SelectAgents(criteria, AgentTeam.Empty, 0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("count");
    }
}

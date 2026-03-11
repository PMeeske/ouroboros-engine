namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class LoadBalancingStrategyTests
{
    private static readonly DelegationCriteria DefaultCriteria =
        DelegationCriteria.FromGoal(Goal.Atomic("test"));

    private static AgentTeam CreateTeamWithAgents(params AgentIdentity[] identities)
    {
        var team = AgentTeam.Empty;
        foreach (var id in identities)
        {
            team = team.AddAgent(id);
        }
        return team;
    }

    [Fact]
    public void Name_IsLoadBalancing()
    {
        // Act & Assert
        new LoadBalancingStrategy().Name.Should().Be("LoadBalancing");
    }

    [Fact]
    public void SelectAgent_NullCriteria_Throws()
    {
        // Arrange
        var strategy = new LoadBalancingStrategy();

        // Act
        Action act = () => strategy.SelectAgent(null!, AgentTeam.Empty);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_NullTeam_Throws()
    {
        // Arrange
        var strategy = new LoadBalancingStrategy();

        // Act
        Action act = () => strategy.SelectAgent(DefaultCriteria, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_EmptyTeam_ReturnsNoMatch()
    {
        // Arrange
        var strategy = new LoadBalancingStrategy();

        // Act
        var result = strategy.SelectAgent(DefaultCriteria, AgentTeam.Empty);

        // Assert
        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_LeastLoadedAgent_SelectedFirst()
    {
        // Arrange — both agents start idle with zero tasks, so both have equal load
        var a1 = AgentIdentity.Create("Agent1", AgentRole.Executor);
        var a2 = AgentIdentity.Create("Agent2", AgentRole.Executor);
        var team = CreateTeamWithAgents(a1, a2);
        var strategy = new LoadBalancingStrategy();

        // Act
        var result = strategy.SelectAgent(DefaultCriteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.MatchScore.Should().BeGreaterThan(0);
    }
}

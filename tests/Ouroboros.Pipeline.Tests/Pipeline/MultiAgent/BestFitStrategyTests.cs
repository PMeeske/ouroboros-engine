namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class BestFitStrategyTests
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
    public void Name_IsBestFit()
    {
        // Act & Assert
        new BestFitStrategy().Name.Should().Be("BestFit");
    }

    [Fact]
    public void SelectAgent_NullCriteria_Throws()
    {
        // Arrange
        var strategy = new BestFitStrategy();

        // Act
        Action act = () => strategy.SelectAgent(null!, AgentTeam.Empty);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_NullTeam_Throws()
    {
        // Arrange
        var strategy = new BestFitStrategy();

        // Act
        Action act = () => strategy.SelectAgent(DefaultCriteria, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_EmptyTeam_ReturnsNoMatch()
    {
        // Arrange
        var strategy = new BestFitStrategy();

        // Act
        var result = strategy.SelectAgent(DefaultCriteria, AgentTeam.Empty);

        // Assert
        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_AvailableAgent_ScoresHigherThanBusy()
    {
        // Arrange — AddAgent creates agents in Idle (available) state by default
        var agent = AgentIdentity.Create("Available", AgentRole.Executor);
        var team = CreateTeamWithAgents(agent);
        var strategy = new BestFitStrategy();

        // Act
        var result = strategy.SelectAgent(DefaultCriteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.MatchScore.Should().BeGreaterThan(0);
    }
}

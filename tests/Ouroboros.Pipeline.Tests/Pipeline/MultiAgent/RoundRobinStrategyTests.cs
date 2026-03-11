namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class RoundRobinStrategyTests
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
    public void Name_IsRoundRobin()
    {
        // Act & Assert
        new RoundRobinStrategy().Name.Should().Be("RoundRobin");
    }

    [Fact]
    public void SelectAgent_NullCriteria_Throws()
    {
        // Arrange
        var strategy = new RoundRobinStrategy();

        // Act
        Action act = () => strategy.SelectAgent(null!, AgentTeam.Empty);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_NullTeam_Throws()
    {
        // Arrange
        var strategy = new RoundRobinStrategy();

        // Act
        Action act = () => strategy.SelectAgent(DefaultCriteria, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgents_ZeroCount_Throws()
    {
        // Arrange
        var strategy = new RoundRobinStrategy();
        var team = CreateTeamWithAgents(AgentIdentity.Create("A", AgentRole.Executor));

        // Act
        Action act = () => strategy.SelectAgents(DefaultCriteria, team, 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SelectAgent_EmptyTeam_ReturnsNoMatch()
    {
        // Arrange
        var strategy = new RoundRobinStrategy();

        // Act
        var result = strategy.SelectAgent(DefaultCriteria, AgentTeam.Empty);

        // Assert
        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_CyclesThroughAgents()
    {
        // Arrange
        var a1 = AgentIdentity.Create("Alpha", AgentRole.Executor);
        var a2 = AgentIdentity.Create("Beta", AgentRole.Executor);
        var team = CreateTeamWithAgents(a1, a2);
        var strategy = new RoundRobinStrategy();

        // Act
        var r1 = strategy.SelectAgent(DefaultCriteria, team);
        var r2 = strategy.SelectAgent(DefaultCriteria, team);

        // Assert
        r1.SelectedAgentId.Should().NotBe(r2.SelectedAgentId);
    }

    [Fact]
    public void Reset_ResetsIndex()
    {
        // Arrange
        var a1 = AgentIdentity.Create("Alpha", AgentRole.Executor);
        var a2 = AgentIdentity.Create("Beta", AgentRole.Executor);
        var team = CreateTeamWithAgents(a1, a2);
        var strategy = new RoundRobinStrategy();

        var first = strategy.SelectAgent(DefaultCriteria, team);
        strategy.SelectAgent(DefaultCriteria, team); // advance

        // Act
        strategy.Reset();
        var afterReset = strategy.SelectAgent(DefaultCriteria, team);

        // Assert
        afterReset.SelectedAgentId.Should().Be(first.SelectedAgentId);
    }

    [Fact]
    public void SelectAgents_ReturnsCorrectCount()
    {
        // Arrange
        var a1 = AgentIdentity.Create("A", AgentRole.Executor);
        var a2 = AgentIdentity.Create("B", AgentRole.Executor);
        var a3 = AgentIdentity.Create("C", AgentRole.Executor);
        var team = CreateTeamWithAgents(a1, a2, a3);
        var strategy = new RoundRobinStrategy();

        // Act
        var results = strategy.SelectAgents(DefaultCriteria, team, 2);

        // Assert
        results.Should().HaveCount(2);
    }
}

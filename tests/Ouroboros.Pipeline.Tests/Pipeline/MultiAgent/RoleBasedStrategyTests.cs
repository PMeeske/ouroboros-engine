namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class RoleBasedStrategyTests
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
    public void Name_IsRoleBased()
    {
        // Act & Assert
        new RoleBasedStrategy().Name.Should().Be("RoleBased");
    }

    [Fact]
    public void SelectAgent_NullCriteria_Throws()
    {
        // Arrange
        var strategy = new RoleBasedStrategy();

        // Act
        Action act = () => strategy.SelectAgent(null!, AgentTeam.Empty);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_NullTeam_Throws()
    {
        // Arrange
        var strategy = new RoleBasedStrategy();

        // Act
        Action act = () => strategy.SelectAgent(DefaultCriteria, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_NoPreferredRole_DelegatesToCapabilityStrategy()
    {
        // Arrange
        var agent = AgentIdentity.Create("Agent", AgentRole.Executor);
        var team = CreateTeamWithAgents(agent);
        var strategy = new RoleBasedStrategy();

        // Act — DefaultCriteria has no PreferredRole
        var result = strategy.SelectAgent(DefaultCriteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_AgentWithMatchingRole_GetsBonus()
    {
        // Arrange
        var planner = AgentIdentity.Create("Planner", AgentRole.Planner);
        var executor = AgentIdentity.Create("Executor", AgentRole.Executor);
        var team = CreateTeamWithAgents(planner, executor);

        var criteria = DefaultCriteria.WithPreferredRole(AgentRole.Planner);
        var strategy = new RoleBasedStrategy();

        // Act
        var result = strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(planner.Id);
    }
}

namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class CapabilityBasedStrategyTests
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
    public void Name_IsCapabilityBased()
    {
        // Act & Assert
        new CapabilityBasedStrategy().Name.Should().Be("CapabilityBased");
    }

    [Fact]
    public void SelectAgent_NullCriteria_Throws()
    {
        // Arrange
        var strategy = new CapabilityBasedStrategy();

        // Act
        Action act = () => strategy.SelectAgent(null!, AgentTeam.Empty);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_NullTeam_Throws()
    {
        // Arrange
        var strategy = new CapabilityBasedStrategy();

        // Act
        Action act = () => strategy.SelectAgent(DefaultCriteria, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_EmptyTeam_ReturnsNoMatch()
    {
        // Arrange
        var strategy = new CapabilityBasedStrategy();

        // Act
        var result = strategy.SelectAgent(DefaultCriteria, AgentTeam.Empty);

        // Assert
        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_AgentWithMatchingCapability_ScoresHigher()
    {
        // Arrange
        var skilled = AgentIdentity.Create("Skilled", AgentRole.Executor)
            .WithCapability(AgentCapability.Create("coding", "Code writing", 0.9));
        var unskilled = AgentIdentity.Create("Unskilled", AgentRole.Executor);

        var team = CreateTeamWithAgents(skilled, unskilled);
        var criteria = DefaultCriteria.RequireCapability("coding");
        var strategy = new CapabilityBasedStrategy();

        // Act
        var result = strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(skilled.Id);
    }

    [Fact]
    public void SelectAgent_NoCapabilitiesRequired_UsesSuccessRate()
    {
        // Arrange
        var agent = AgentIdentity.Create("Agent", AgentRole.Executor);
        var team = CreateTeamWithAgents(agent);
        var strategy = new CapabilityBasedStrategy();

        // Act
        var result = strategy.SelectAgent(DefaultCriteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
    }
}

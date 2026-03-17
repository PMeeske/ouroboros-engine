using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class RoleBasedStrategyTests
{
    private readonly RoleBasedStrategy _strategy = new();

    [Fact]
    public void Name_ReturnsRoleBased()
    {
        _strategy.Name.Should().Be("RoleBased");
    }

    [Fact]
    public void SelectAgent_WithEmptyTeam_ReturnsNoMatch()
    {
        var criteria = StrategyTestHelpers.CreateCriteria(preferredRole: AgentRole.Coder);
        var result = _strategy.SelectAgent(criteria, AgentTeam.Empty);
        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_PrefersMatchingRole()
    {
        // Arrange
        var coder = StrategyTestHelpers.CreateIdentity("Coder", AgentRole.Coder);
        var analyst = StrategyTestHelpers.CreateIdentity("Analyst", AgentRole.Analyst);
        var team = StrategyTestHelpers.CreateTeamWithAgents(coder, analyst);
        var criteria = StrategyTestHelpers.CreateCriteria(preferredRole: AgentRole.Coder);

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(coder.Id);
        result.Reasoning.Should().Contain("matching role");
    }

    [Fact]
    public void SelectAgent_FallsBackWhenNoRoleMatch()
    {
        // Arrange - no planner in team
        var coder = StrategyTestHelpers.CreateIdentity("Coder", AgentRole.Coder);
        var team = StrategyTestHelpers.CreateTeamWithAgents(coder);
        var criteria = StrategyTestHelpers.CreateCriteria(preferredRole: AgentRole.Planner);

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.Reasoning.Should().Contain("fallback");
    }

    [Fact]
    public void SelectAgent_WithoutPreferredRole_DelegatesToCapabilityStrategy()
    {
        // Arrange
        var agent = StrategyTestHelpers.CreateIdentity("Agent", AgentRole.Coder, ("coding", 0.9));
        var team = StrategyTestHelpers.CreateTeamWithAgents(agent);
        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding");

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_AppliesRoleBonus()
    {
        // Arrange - coder with lower base score but role match should still win
        var coder = StrategyTestHelpers.CreateIdentity("Coder", AgentRole.Coder);
        var analyst = StrategyTestHelpers.CreateIdentity("Analyst", AgentRole.Analyst);
        var team = StrategyTestHelpers.CreateTeamWithAgents(coder, analyst);
        var criteria = StrategyTestHelpers.CreateCriteria(preferredRole: AgentRole.Coder);

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.SelectedAgentId.Should().Be(coder.Id);
    }

    [Fact]
    public void SelectAgents_ReturnsMultipleResults()
    {
        // Arrange
        var c1 = StrategyTestHelpers.CreateIdentity("Coder1", AgentRole.Coder);
        var c2 = StrategyTestHelpers.CreateIdentity("Coder2", AgentRole.Coder);
        var team = StrategyTestHelpers.CreateTeamWithAgents(c1, c2);
        var criteria = StrategyTestHelpers.CreateCriteria(preferredRole: AgentRole.Coder);

        // Act
        var results = _strategy.SelectAgents(criteria, team, 2);

        // Assert
        results.Should().HaveCount(2);
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

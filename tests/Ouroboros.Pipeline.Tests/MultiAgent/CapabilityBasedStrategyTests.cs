using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class CapabilityBasedStrategyTests
{
    private readonly CapabilityBasedStrategy _strategy = new();

    [Fact]
    public void Name_ReturnsCapabilityBased()
    {
        _strategy.Name.Should().Be("CapabilityBased");
    }

    [Fact]
    public void SelectAgent_WithEmptyTeam_ReturnsNoMatch()
    {
        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding");
        var result = _strategy.SelectAgent(criteria, AgentTeam.Empty);
        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_SelectsHighestProficiencyAgent()
    {
        // Arrange
        var lowSkill = StrategyTestHelpers.CreateIdentity("Low", AgentRole.Coder, ("coding", 0.3));
        var highSkill = StrategyTestHelpers.CreateIdentity("High", AgentRole.Coder, ("coding", 0.9));
        var team = StrategyTestHelpers.CreateTeamWithAgents(lowSkill, highSkill);
        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding");

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(highSkill.Id);
    }

    [Fact]
    public void SelectAgent_WithNoRequiredCapabilities_UsesSuccessRate()
    {
        // Arrange
        var agent1 = StrategyTestHelpers.CreateIdentity("Agent1", AgentRole.Coder);
        var team = StrategyTestHelpers.CreateTeamWithAgents(agent1);
        var criteria = StrategyTestHelpers.CreateCriteria();

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
    }

    [Fact]
    public void SelectAgent_AppliesAvailabilityBonus()
    {
        // Arrange - two agents with same capability, one available, one busy
        var available = StrategyTestHelpers.CreateIdentity("Available", AgentRole.Coder, ("coding", 0.7));
        var busy = StrategyTestHelpers.CreateIdentity("Busy", AgentRole.Coder, ("coding", 0.7));
        var team = StrategyTestHelpers.CreateTeamWithAgents(available, busy);

        // Make the busy agent busy
        var busyState = team.GetAgent(busy.Id).Value!.StartTask(Guid.NewGuid());
        team = team.UpdateAgent(busy.Id, busyState);

        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding");

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert - available agent should be selected due to availability bonus
        result.SelectedAgentId.Should().Be(available.Id);
    }

    [Fact]
    public void SelectAgent_FiltersAgentsBelowMinProficiency()
    {
        // Arrange
        var low = StrategyTestHelpers.CreateIdentity("Low", AgentRole.Coder, ("coding", 0.3));
        var high = StrategyTestHelpers.CreateIdentity("High", AgentRole.Coder, ("coding", 0.9));
        var team = StrategyTestHelpers.CreateTeamWithAgents(low, high);
        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding", minProficiency: 0.5);

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(high.Id);
    }

    [Fact]
    public void SelectAgent_WhenNoAgentsMeetMinProficiency_ReturnsNoMatch()
    {
        // Arrange
        var low = StrategyTestHelpers.CreateIdentity("Low", AgentRole.Coder, ("coding", 0.2));
        var team = StrategyTestHelpers.CreateTeamWithAgents(low);
        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding", minProficiency: 0.9);

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgents_ReturnsAlternativesForFirstResult()
    {
        // Arrange
        var agent1 = StrategyTestHelpers.CreateIdentity("A1", AgentRole.Coder, ("coding", 0.9));
        var agent2 = StrategyTestHelpers.CreateIdentity("A2", AgentRole.Coder, ("coding", 0.7));
        var team = StrategyTestHelpers.CreateTeamWithAgents(agent1, agent2);
        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding");

        // Act
        var results = _strategy.SelectAgents(criteria, team, 1);

        // Assert
        results.Should().HaveCount(1);
        results[0].Alternatives.Should().NotBeEmpty();
    }

    [Fact]
    public void SelectAgent_WithNullCriteria_ThrowsArgumentNullException()
    {
        Action act = () => _strategy.SelectAgent(null!, AgentTeam.Empty);
        act.Should().Throw<ArgumentNullException>().WithParameterName("criteria");
    }

    [Fact]
    public void SelectAgent_WithNullTeam_ThrowsArgumentNullException()
    {
        var criteria = StrategyTestHelpers.CreateCriteria();
        Action act = () => _strategy.SelectAgent(criteria, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("team");
    }
}

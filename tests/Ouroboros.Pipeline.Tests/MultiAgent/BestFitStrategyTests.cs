using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class BestFitStrategyTests
{
    private readonly BestFitStrategy _strategy = new();

    [Fact]
    public void Name_ReturnsBestFit()
    {
        _strategy.Name.Should().Be("BestFit");
    }

    [Fact]
    public void SelectAgent_WithEmptyTeam_ReturnsNoMatch()
    {
        var criteria = StrategyTestHelpers.CreateCriteria();
        var result = _strategy.SelectAgent(criteria, AgentTeam.Empty);
        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public void SelectAgent_ConsidersMultipleFactors()
    {
        // Arrange - agent with high capability should score higher
        var highCap = StrategyTestHelpers.CreateIdentity("HighCap", AgentRole.Coder, ("coding", 0.95));
        var lowCap = StrategyTestHelpers.CreateIdentity("LowCap", AgentRole.Analyst, ("coding", 0.2));
        var team = StrategyTestHelpers.CreateTeamWithAgents(highCap, lowCap);
        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding", preferredRole: AgentRole.Coder);

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(highCap.Id);
        result.MatchScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SelectAgent_GivesAvailabilityBonus()
    {
        // Arrange
        var available = StrategyTestHelpers.CreateIdentity("Available", AgentRole.Coder);
        var busy = StrategyTestHelpers.CreateIdentity("Busy", AgentRole.Coder);
        var team = StrategyTestHelpers.CreateTeamWithAgents(available, busy);

        // Make busy agent busy
        var busyState = team.GetAgent(busy.Id).Value!.StartTask(Guid.NewGuid());
        team = team.UpdateAgent(busy.Id, busyState);

        var criteria = StrategyTestHelpers.CreateCriteria();

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert - available agent should be preferred
        result.SelectedAgentId.Should().Be(available.Id);
    }

    [Fact]
    public void SelectAgent_GivesRoleMatchBonus()
    {
        // Arrange - two agents identical except role
        var coder = StrategyTestHelpers.CreateIdentity("Coder", AgentRole.Coder, ("coding", 0.5));
        var analyst = StrategyTestHelpers.CreateIdentity("Analyst", AgentRole.Analyst, ("coding", 0.5));
        var team = StrategyTestHelpers.CreateTeamWithAgents(coder, analyst);
        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding", preferredRole: AgentRole.Coder);

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.SelectedAgentId.Should().Be(coder.Id);
    }

    [Fact]
    public void SelectAgents_ReturnsOrderedByScore()
    {
        // Arrange
        var high = StrategyTestHelpers.CreateIdentity("High", AgentRole.Coder, ("coding", 0.9));
        var mid = StrategyTestHelpers.CreateIdentity("Mid", AgentRole.Coder, ("coding", 0.5));
        var low = StrategyTestHelpers.CreateIdentity("Low", AgentRole.Coder, ("coding", 0.2));
        var team = StrategyTestHelpers.CreateTeamWithAgents(high, mid, low);
        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding");

        // Act
        var results = _strategy.SelectAgents(criteria, team, 3);

        // Assert
        results.Should().HaveCount(3);
        for (int i = 0; i < results.Count - 1; i++)
        {
            results[i].MatchScore.Should().BeGreaterThanOrEqualTo(results[i + 1].MatchScore);
        }
    }

    [Fact]
    public void SelectAgents_FirstResultIncludesAlternatives()
    {
        // Arrange
        var a1 = StrategyTestHelpers.CreateIdentity("A1", AgentRole.Coder, ("coding", 0.9));
        var a2 = StrategyTestHelpers.CreateIdentity("A2", AgentRole.Coder, ("coding", 0.7));
        var team = StrategyTestHelpers.CreateTeamWithAgents(a1, a2);
        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding");

        // Act
        var results = _strategy.SelectAgents(criteria, team, 1);

        // Assert
        results[0].Alternatives.Should().NotBeEmpty();
    }

    [Fact]
    public void SelectAgent_ReasoningContainsBreakdown()
    {
        // Arrange
        var agent = StrategyTestHelpers.CreateIdentity("Agent", AgentRole.Coder, ("coding", 0.8));
        var team = StrategyTestHelpers.CreateTeamWithAgents(agent);
        var criteria = StrategyTestHelpers.CreateCriteria(requiredCapability: "coding");

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.Reasoning.Should().Contain("best-fit");
        result.Reasoning.Should().Contain("Capability=");
        result.Reasoning.Should().Contain("Availability=");
    }

    [Fact]
    public void SelectAgents_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        var criteria = StrategyTestHelpers.CreateCriteria();
        Action act = () => _strategy.SelectAgents(criteria, AgentTeam.Empty, -1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("count");
    }

    [Fact]
    public void SelectAgent_WithNoCapabilitiesRequired_UsesNeutralScore()
    {
        // Arrange - agent without any capabilities gets neutral 0.5 for capability score
        var agent = StrategyTestHelpers.CreateIdentity("Agent", AgentRole.Coder);
        var team = StrategyTestHelpers.CreateTeamWithAgents(agent);
        var criteria = StrategyTestHelpers.CreateCriteria();

        // Act
        var result = _strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.MatchScore.Should().BeGreaterThan(0);
    }
}

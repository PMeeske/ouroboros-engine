namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class DelegationResultTests
{
    [Fact]
    public void Success_SetsProperties()
    {
        var agentId = Guid.NewGuid();
        var result = DelegationResult.Success(agentId, "Best match", 0.9);

        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(agentId);
        result.Reasoning.Should().Be("Best match");
        result.MatchScore.Should().Be(0.9);
        result.Alternatives.Should().BeEmpty();
    }

    [Fact]
    public void Success_AcceptsAlternatives()
    {
        var alternatives = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var result = DelegationResult.Success(Guid.NewGuid(), "match", 0.8, alternatives);

        result.Alternatives.Should().HaveCount(2);
    }

    [Fact]
    public void Success_ThrowsOnInvalidScore()
    {
        var act = () => DelegationResult.Success(Guid.NewGuid(), "reason", 1.5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NoMatch_HasNoSelectedAgent()
    {
        var result = DelegationResult.NoMatch("No suitable agent found");

        result.HasMatch.Should().BeFalse();
        result.SelectedAgentId.Should().BeNull();
        result.MatchScore.Should().Be(0.0);
        result.Reasoning.Should().Contain("No suitable agent");
    }

    [Fact]
    public void NoMatch_ThrowsOnNullReason()
    {
        var act = () => DelegationResult.NoMatch(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class DelegationResultTests
{
    [Fact]
    public void Success_WithValidParams_ReturnsMatchingResult()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var alternatives = new List<Guid> { Guid.NewGuid() };

        // Act
        var result = DelegationResult.Success(agentId, "Best match", 0.95, alternatives);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(agentId);
        result.Reasoning.Should().Be("Best match");
        result.MatchScore.Should().Be(0.95);
        result.Alternatives.Should().HaveCount(1);
    }

    [Fact]
    public void Success_WithoutAlternatives_ReturnsEmptyAlternatives()
    {
        var result = DelegationResult.Success(Guid.NewGuid(), "Match", 0.8);
        result.Alternatives.Should().BeEmpty();
    }

    [Fact]
    public void Success_WithNullReasoning_ThrowsArgumentNullException()
    {
        Action act = () => DelegationResult.Success(Guid.NewGuid(), null!, 0.5);
        act.Should().Throw<ArgumentNullException>().WithParameterName("reasoning");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Success_WithInvalidScore_ThrowsArgumentOutOfRangeException(double score)
    {
        Action act = () => DelegationResult.Success(Guid.NewGuid(), "reason", score);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("score");
    }

    [Fact]
    public void NoMatch_ReturnsResultWithoutMatch()
    {
        var result = DelegationResult.NoMatch("No suitable agent found");
        result.HasMatch.Should().BeFalse();
        result.SelectedAgentId.Should().BeNull();
        result.MatchScore.Should().Be(0.0);
        result.Reasoning.Should().Be("No suitable agent found");
        result.Alternatives.Should().BeEmpty();
    }

    [Fact]
    public void NoMatch_WithNullReason_ThrowsArgumentNullException()
    {
        Action act = () => DelegationResult.NoMatch(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("reason");
    }
}

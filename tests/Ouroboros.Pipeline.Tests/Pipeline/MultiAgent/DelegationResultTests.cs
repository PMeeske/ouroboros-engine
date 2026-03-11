namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class DelegationResultTests
{
    [Fact]
    public void Success_CreatesResultWithHasMatchTrue()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var result = DelegationResult.Success(agentId, "Best match", 0.9);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(agentId);
        result.Reasoning.Should().Be("Best match");
        result.MatchScore.Should().Be(0.9);
    }

    [Fact]
    public void Success_WithNullAlternatives_DefaultsToEmpty()
    {
        // Act
        var result = DelegationResult.Success(Guid.NewGuid(), "match", 0.8, null);

        // Assert
        result.Alternatives.Should().BeEmpty();
    }

    [Fact]
    public void Success_ThrowsOnNullReasoning()
    {
        // Act
        Action act = () => DelegationResult.Success(Guid.NewGuid(), null!, 0.5);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Success_ThrowsOnScoreLessThanZero()
    {
        // Act
        Action act = () => DelegationResult.Success(Guid.NewGuid(), "reason", -0.1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Success_ThrowsOnScoreGreaterThanOne()
    {
        // Act
        Action act = () => DelegationResult.Success(Guid.NewGuid(), "reason", 1.1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NoMatch_CreatesResultWithHasMatchFalse()
    {
        // Act
        var result = DelegationResult.NoMatch("No suitable agent found");

        // Assert
        result.HasMatch.Should().BeFalse();
        result.SelectedAgentId.Should().BeNull();
        result.MatchScore.Should().Be(0.0);
        result.Reasoning.Should().Contain("No suitable agent");
    }

    [Fact]
    public void NoMatch_ThrowsOnNullReason()
    {
        // Act
        Action act = () => DelegationResult.NoMatch(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HasMatch_IsFalse_WhenSelectedAgentIdIsNull()
    {
        // Arrange
        var result = new DelegationResult(null, "no match", 0.0, Array.Empty<Guid>());

        // Act & Assert
        result.HasMatch.Should().BeFalse();
    }
}

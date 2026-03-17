using Ouroboros.Pipeline.Council;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class DebateRoundTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var contributions = new List<AgentContribution>
        {
            new("Agent1", "Content1"),
            new("Agent2", "Content2")
        };
        var timestamp = DateTime.UtcNow;

        // Act
        var round = new DebateRound(
            DebatePhase.Proposal,
            1,
            contributions,
            timestamp);

        // Assert
        round.Phase.Should().Be(DebatePhase.Proposal);
        round.RoundNumber.Should().Be(1);
        round.Contributions.Should().HaveCount(2);
        round.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void Constructor_WithEmptyContributions_IsValid()
    {
        // Act
        var round = new DebateRound(
            DebatePhase.Voting,
            1,
            new List<AgentContribution>(),
            DateTime.UtcNow);

        // Assert
        round.Contributions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(DebatePhase.Proposal)]
    [InlineData(DebatePhase.Challenge)]
    [InlineData(DebatePhase.Refinement)]
    [InlineData(DebatePhase.Voting)]
    [InlineData(DebatePhase.Synthesis)]
    public void Phase_AcceptsAllDebatePhases(DebatePhase phase)
    {
        // Act
        var round = new DebateRound(phase, 1, [], DateTime.UtcNow);

        // Assert
        round.Phase.Should().Be(phase);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        IReadOnlyList<AgentContribution> contributions = [];

        // Act
        var round1 = new DebateRound(DebatePhase.Proposal, 1, contributions, timestamp);
        var round2 = new DebateRound(DebatePhase.Proposal, 1, contributions, timestamp);

        // Assert
        round1.Should().Be(round2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = new DebateRound(DebatePhase.Proposal, 1, [], DateTime.UtcNow);

        // Act
        var modified = original with { RoundNumber = 2 };

        // Assert
        modified.Phase.Should().Be(DebatePhase.Proposal);
        modified.RoundNumber.Should().Be(2);
    }
}

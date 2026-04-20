using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class ConsensusResultTests
{
    [Fact]
    public void NoConsensus_ReturnsResultWithoutConsensus()
    {
        // Arrange
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "B", 0.7)
        };

        // Act
        var result = ConsensusResult.NoConsensus(votes, "Majority");

        // Assert
        result.HasConsensus.Should().BeFalse();
        result.WinningOption.Should().BeEmpty();
        result.AggregateConfidence.Should().Be(0.0);
        result.AllVotes.Should().HaveCount(2);
        result.Protocol.Should().Be("Majority");
    }

    [Fact]
    public void NoConsensus_WithNullVotes_ThrowsArgumentNullException()
    {
        Action act = () => ConsensusResult.NoConsensus(null!, "Majority");
        act.Should().Throw<ArgumentNullException>().WithParameterName("votes");
    }

    [Fact]
    public void NoConsensus_WithNullProtocol_ThrowsArgumentNullException()
    {
        Action act = () => ConsensusResult.NoConsensus(new List<AgentVote>(), null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("protocol");
    }

    [Fact]
    public void TotalVotes_ReturnsCorrectCount()
    {
        // Arrange
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "A", 0.9),
            AgentVote.Create(Guid.NewGuid(), "B", 0.7)
        };
        var result = ConsensusResult.NoConsensus(votes, "Majority");

        // Assert
        result.TotalVotes.Should().Be(3);
    }

    [Fact]
    public void ParticipationRate_WithValidTotalAgents_ReturnsCorrectRate()
    {
        // Arrange
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "B", 0.7)
        };
        var result = ConsensusResult.NoConsensus(votes, "Majority");

        // Act & Assert
        result.ParticipationRate(4).Should().Be(0.5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ParticipationRate_WithInvalidTotalAgents_ThrowsArgumentOutOfRangeException(int totalAgents)
    {
        var result = ConsensusResult.NoConsensus(new List<AgentVote>(), "Majority");
        Action act = () => result.ParticipationRate(totalAgents);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("totalAgents");
    }

    [Fact]
    public void VoteCounts_CalculatedCorrectly()
    {
        // Arrange
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "A", 0.9),
            AgentVote.Create(Guid.NewGuid(), "B", 0.7)
        };
        var result = ConsensusResult.NoConsensus(votes, "Majority");

        // Assert
        result.VoteCounts.Should().ContainKey("A").WhoseValue.Should().Be(2);
        result.VoteCounts.Should().ContainKey("B").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ConfidenceByOption_AggregatesCorrectly()
    {
        // Arrange
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "A", 0.7),
            AgentVote.Create(Guid.NewGuid(), "B", 0.5)
        };
        var result = ConsensusResult.NoConsensus(votes, "Majority");

        // Assert
        result.ConfidenceByOption["A"].Should().BeApproximately(1.5, 0.001);
        result.ConfidenceByOption["B"].Should().BeApproximately(0.5, 0.001);
    }
}

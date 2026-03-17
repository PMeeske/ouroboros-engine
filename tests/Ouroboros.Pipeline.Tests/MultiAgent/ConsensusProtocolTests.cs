using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class ConsensusProtocolTests
{
    #region Construction

    [Fact]
    public void Constructor_WithValidThreshold_Succeeds()
    {
        var protocol = new ConsensusProtocol(ConsensusStrategy.Majority, 0.6);
        protocol.Strategy.Should().Be(ConsensusStrategy.Majority);
        protocol.Threshold.Should().Be(0.6);
    }

    [Fact]
    public void Constructor_WithDefaultThreshold_Uses05()
    {
        var protocol = new ConsensusProtocol(ConsensusStrategy.Majority);
        protocol.Threshold.Should().Be(0.5);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_WithInvalidThreshold_ThrowsArgumentOutOfRangeException(double threshold)
    {
        Action act = () => new ConsensusProtocol(ConsensusStrategy.Majority, threshold);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("threshold");
    }

    [Fact]
    public void StaticInstances_AreCorrectlyConfigured()
    {
        ConsensusProtocol.Majority.Strategy.Should().Be(ConsensusStrategy.Majority);
        ConsensusProtocol.SuperMajority.Strategy.Should().Be(ConsensusStrategy.SuperMajority);
        ConsensusProtocol.Unanimous.Strategy.Should().Be(ConsensusStrategy.Unanimous);
        ConsensusProtocol.WeightedByConfidence.Strategy.Should().Be(ConsensusStrategy.WeightedByConfidence);
        ConsensusProtocol.HighestConfidence.Strategy.Should().Be(ConsensusStrategy.HighestConfidence);
    }

    #endregion

    #region Evaluate - Empty votes

    [Fact]
    public void Evaluate_WithEmptyVotes_ReturnsNoConsensus()
    {
        var result = ConsensusProtocol.Majority.Evaluate(new List<AgentVote>());
        result.HasConsensus.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WithNullVotes_ThrowsArgumentNullException()
    {
        Action act = () => ConsensusProtocol.Majority.Evaluate(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("votes");
    }

    #endregion

    #region Majority

    [Fact]
    public void Evaluate_Majority_WhenMajorityExists_ReturnsConsensus()
    {
        // Arrange - 3 votes for A, 1 for B -> 75% > 50%
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "A", 0.7),
            AgentVote.Create(Guid.NewGuid(), "A", 0.9),
            AgentVote.Create(Guid.NewGuid(), "B", 0.6),
        };

        // Act
        var result = ConsensusProtocol.Majority.Evaluate(votes);

        // Assert
        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("A");
        result.Protocol.Should().Be("Majority");
    }

    [Fact]
    public void Evaluate_Majority_WhenTied_ReturnsNoConsensus()
    {
        // Arrange - 50/50 split - not > 50%
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "B", 0.7),
        };

        // Act
        var result = ConsensusProtocol.Majority.Evaluate(votes);

        // Assert
        result.HasConsensus.Should().BeFalse();
    }

    #endregion

    #region SuperMajority

    [Fact]
    public void Evaluate_SuperMajority_When67Percent_ReturnsConsensus()
    {
        // Arrange - 3 votes for A, 1 for B -> 75% > 66.67%
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "A", 0.7),
            AgentVote.Create(Guid.NewGuid(), "A", 0.9),
            AgentVote.Create(Guid.NewGuid(), "B", 0.6),
        };

        var result = ConsensusProtocol.SuperMajority.Evaluate(votes);
        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("A");
    }

    [Fact]
    public void Evaluate_SuperMajority_WhenBelow67Percent_ReturnsNoConsensus()
    {
        // Arrange - 2 votes for A, 1 for B -> 66.67% not > 66.67%
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "A", 0.7),
            AgentVote.Create(Guid.NewGuid(), "B", 0.6),
        };

        var result = ConsensusProtocol.SuperMajority.Evaluate(votes);
        result.HasConsensus.Should().BeFalse();
    }

    #endregion

    #region Unanimous

    [Fact]
    public void Evaluate_Unanimous_WhenAllAgree_ReturnsConsensus()
    {
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "A", 0.9),
            AgentVote.Create(Guid.NewGuid(), "A", 0.7),
        };

        var result = ConsensusProtocol.Unanimous.Evaluate(votes);
        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("A");
    }

    [Fact]
    public void Evaluate_Unanimous_WhenNotAllAgree_ReturnsNoConsensus()
    {
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "B", 0.9),
        };

        var result = ConsensusProtocol.Unanimous.Evaluate(votes);
        result.HasConsensus.Should().BeFalse();
    }

    #endregion

    #region WeightedByConfidence

    [Fact]
    public void Evaluate_WeightedByConfidence_SelectsHighestWeightedOption()
    {
        // Arrange - A has higher total confidence-weighted ratio
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.9),
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "B", 0.3),
        };

        var result = ConsensusProtocol.WeightedByConfidence.Evaluate(votes);
        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("A");
    }

    [Fact]
    public void Evaluate_WeightedByConfidence_WithZeroConfidence_ReturnsNoConsensus()
    {
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.0),
            AgentVote.Create(Guid.NewGuid(), "B", 0.0),
        };

        var result = ConsensusProtocol.WeightedByConfidence.Evaluate(votes);
        result.HasConsensus.Should().BeFalse();
    }

    #endregion

    #region HighestConfidence

    [Fact]
    public void Evaluate_HighestConfidence_SelectsMostConfidentVote()
    {
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.5),
            AgentVote.Create(Guid.NewGuid(), "B", 0.95),
            AgentVote.Create(Guid.NewGuid(), "A", 0.7),
        };

        var result = ConsensusProtocol.HighestConfidence.Evaluate(votes);
        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("B");
        result.AggregateConfidence.Should().Be(0.95);
    }

    #endregion

    #region RankedChoice

    [Fact]
    public void Evaluate_RankedChoice_WithMajority_ReturnsWinner()
    {
        // Arrange - A has majority
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "A", 0.7),
            AgentVote.Create(Guid.NewGuid(), "B", 0.9),
        };

        var protocol = new ConsensusProtocol(ConsensusStrategy.RankedChoice);
        var result = protocol.Evaluate(votes);
        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("A");
    }

    [Fact]
    public void Evaluate_RankedChoice_WithNoMajority_UsesConfidence()
    {
        // Arrange - no majority, B has higher confidence
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.3),
            AgentVote.Create(Guid.NewGuid(), "B", 0.9),
            AgentVote.Create(Guid.NewGuid(), "C", 0.5),
            AgentVote.Create(Guid.NewGuid(), "D", 0.4),
        };

        var protocol = new ConsensusProtocol(ConsensusStrategy.RankedChoice);
        var result = protocol.Evaluate(votes);
        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("B");
    }

    #endregion

    #region MeetsThreshold

    [Fact]
    public void MeetsThreshold_WhenAboveThreshold_ReturnsTrue()
    {
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "A", 0.7),
            AgentVote.Create(Guid.NewGuid(), "B", 0.6),
        };

        ConsensusProtocol.Majority.MeetsThreshold(votes, 0.5).Should().BeTrue();
    }

    [Fact]
    public void MeetsThreshold_WhenBelowThreshold_ReturnsFalse()
    {
        var votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.8),
            AgentVote.Create(Guid.NewGuid(), "B", 0.7),
        };

        ConsensusProtocol.Majority.MeetsThreshold(votes, 0.6).Should().BeFalse();
    }

    [Fact]
    public void MeetsThreshold_WithEmptyVotes_ReturnsFalse()
    {
        ConsensusProtocol.Majority.MeetsThreshold(new List<AgentVote>(), 0.5).Should().BeFalse();
    }

    [Fact]
    public void MeetsThreshold_WithNullVotes_ThrowsArgumentNullException()
    {
        Action act = () => ConsensusProtocol.Majority.MeetsThreshold(null!, 0.5);
        act.Should().Throw<ArgumentNullException>().WithParameterName("votes");
    }

    #endregion
}

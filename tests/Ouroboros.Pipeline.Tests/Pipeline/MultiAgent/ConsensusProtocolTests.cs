// <copyright file="ConsensusProtocolTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public sealed class ConsensusProtocolTests
{
    private static AgentVote Vote(string option, double confidence = 0.8)
        => AgentVote.Create(Guid.NewGuid(), option, confidence);

    // --- Constructor ---

    [Fact]
    public void Constructor_NegativeThreshold_Throws()
    {
        Action act = () => _ = new ConsensusProtocol(ConsensusStrategy.Majority, -0.1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ThresholdAboveOne_Throws()
    {
        Action act = () => _ = new ConsensusProtocol(ConsensusStrategy.Majority, 1.1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ValidThreshold_SetsProperties()
    {
        var protocol = new ConsensusProtocol(ConsensusStrategy.Majority, 0.6);
        protocol.Strategy.Should().Be(ConsensusStrategy.Majority);
        protocol.Threshold.Should().Be(0.6);
    }

    // --- Static Instances ---

    [Fact]
    public void Majority_HasCorrectStrategy()
    {
        ConsensusProtocol.Majority.Strategy.Should().Be(ConsensusStrategy.Majority);
    }

    [Fact]
    public void SuperMajority_HasCorrectStrategy()
    {
        ConsensusProtocol.SuperMajority.Strategy.Should().Be(ConsensusStrategy.SuperMajority);
    }

    [Fact]
    public void Unanimous_HasCorrectStrategy()
    {
        ConsensusProtocol.Unanimous.Strategy.Should().Be(ConsensusStrategy.Unanimous);
    }

    [Fact]
    public void WeightedByConfidence_HasCorrectStrategy()
    {
        ConsensusProtocol.WeightedByConfidence.Strategy.Should().Be(ConsensusStrategy.WeightedByConfidence);
    }

    [Fact]
    public void HighestConfidence_HasCorrectStrategy()
    {
        ConsensusProtocol.HighestConfidence.Strategy.Should().Be(ConsensusStrategy.HighestConfidence);
    }

    // --- Evaluate: Empty Votes ---

    [Fact]
    public void Evaluate_NullVotes_Throws()
    {
        Action act = () => ConsensusProtocol.Majority.Evaluate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Evaluate_EmptyVotes_NoConsensus()
    {
        var result = ConsensusProtocol.Majority.Evaluate(Array.Empty<AgentVote>());
        result.HasConsensus.Should().BeFalse();
    }

    // --- Majority ---

    [Fact]
    public void Majority_ClearMajority_ReachesConsensus()
    {
        var votes = new[] { Vote("A"), Vote("A"), Vote("B") };
        var result = ConsensusProtocol.Majority.Evaluate(votes);

        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("A");
    }

    [Fact]
    public void Majority_EvenSplit_NoConsensus()
    {
        var votes = new[] { Vote("A"), Vote("B") };
        var result = ConsensusProtocol.Majority.Evaluate(votes);

        result.HasConsensus.Should().BeFalse();
    }

    // --- SuperMajority ---

    [Fact]
    public void SuperMajority_TwoThirds_ReachesConsensus()
    {
        var votes = new[] { Vote("A"), Vote("A"), Vote("A"), Vote("B") };
        var result = ConsensusProtocol.SuperMajority.Evaluate(votes);

        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("A");
    }

    [Fact]
    public void SuperMajority_BelowThreshold_NoConsensus()
    {
        // 2 out of 3 = 66.6%, not > 66.6%
        var votes = new[] { Vote("A"), Vote("A"), Vote("B") };
        var result = ConsensusProtocol.SuperMajority.Evaluate(votes);

        result.HasConsensus.Should().BeFalse();
    }

    // --- Unanimous ---

    [Fact]
    public void Unanimous_AllAgree_ReachesConsensus()
    {
        var votes = new[] { Vote("A"), Vote("A"), Vote("A") };
        var result = ConsensusProtocol.Unanimous.Evaluate(votes);

        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("A");
    }

    [Fact]
    public void Unanimous_AnyDisagreement_NoConsensus()
    {
        var votes = new[] { Vote("A"), Vote("A"), Vote("B") };
        var result = ConsensusProtocol.Unanimous.Evaluate(votes);

        result.HasConsensus.Should().BeFalse();
    }

    // --- HighestConfidence ---

    [Fact]
    public void HighestConfidence_SelectsHighestConfidenceVote()
    {
        var votes = new[]
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.5),
            AgentVote.Create(Guid.NewGuid(), "B", 0.9),
            AgentVote.Create(Guid.NewGuid(), "A", 0.3),
        };

        var result = ConsensusProtocol.HighestConfidence.Evaluate(votes);

        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("B");
        result.AggregateConfidence.Should().Be(0.9);
    }

    // --- WeightedByConfidence ---

    [Fact]
    public void WeightedByConfidence_HighConfidenceMinority_CanWin()
    {
        var votes = new[]
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.2),
            AgentVote.Create(Guid.NewGuid(), "A", 0.2),
            AgentVote.Create(Guid.NewGuid(), "B", 0.9),
        };

        var result = ConsensusProtocol.WeightedByConfidence.Evaluate(votes);

        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("B");
    }

    // --- RankedChoice ---

    [Fact]
    public void RankedChoice_MajorityExists_SelectsIt()
    {
        var votes = new[] { Vote("A"), Vote("A"), Vote("B") };
        var protocol = new ConsensusProtocol(ConsensusStrategy.RankedChoice);

        var result = protocol.Evaluate(votes);

        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("A");
    }

    [Fact]
    public void RankedChoice_NoMajority_FallsBackToConfidenceWeighted()
    {
        var votes = new[]
        {
            AgentVote.Create(Guid.NewGuid(), "A", 0.9),
            AgentVote.Create(Guid.NewGuid(), "B", 0.1),
        };

        var protocol = new ConsensusProtocol(ConsensusStrategy.RankedChoice);
        var result = protocol.Evaluate(votes);

        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("A");
    }

    // --- MeetsThreshold ---

    [Fact]
    public void MeetsThreshold_NullVotes_Throws()
    {
        Action act = () => ConsensusProtocol.Majority.MeetsThreshold(null!, 0.5);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MeetsThreshold_EmptyVotes_ReturnsFalse()
    {
        ConsensusProtocol.Majority.MeetsThreshold(Array.Empty<AgentVote>(), 0.5)
            .Should().BeFalse();
    }

    [Fact]
    public void MeetsThreshold_AboveThreshold_ReturnsTrue()
    {
        var votes = new[] { Vote("A"), Vote("A"), Vote("B") };
        ConsensusProtocol.Majority.MeetsThreshold(votes, 0.5).Should().BeTrue();
    }

    [Fact]
    public void MeetsThreshold_BelowThreshold_ReturnsFalse()
    {
        var votes = new[] { Vote("A"), Vote("B") };
        ConsensusProtocol.Majority.MeetsThreshold(votes, 0.6).Should().BeFalse();
    }
}

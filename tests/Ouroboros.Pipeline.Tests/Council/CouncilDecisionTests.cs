using Ouroboros.Pipeline.Council;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class CouncilDecisionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var votes = new Dictionary<string, AgentVote>
        {
            ["Agent1"] = new("Agent1", "APPROVE", 0.9, "Good idea"),
            ["Agent2"] = new("Agent2", "APPROVE", 0.8, "Solid plan")
        };
        var transcript = new List<DebateRound>();
        var minorities = new List<MinorityOpinion>();

        // Act
        var decision = new CouncilDecision(
            "We should proceed",
            votes,
            transcript,
            0.85,
            minorities);

        // Assert
        decision.Conclusion.Should().Be("We should proceed");
        decision.Votes.Should().HaveCount(2);
        decision.Transcript.Should().BeEmpty();
        decision.Confidence.Should().Be(0.85);
        decision.MinorityOpinions.Should().BeEmpty();
    }

    [Fact]
    public void IsConsensus_WithAllSamePosition_ReturnsTrue()
    {
        // Arrange
        var votes = new Dictionary<string, AgentVote>
        {
            ["Agent1"] = new("Agent1", "APPROVE", 1.0, "R1"),
            ["Agent2"] = new("Agent2", "APPROVE", 0.9, "R2"),
            ["Agent3"] = new("Agent3", "APPROVE", 0.8, "R3")
        };

        var decision = new CouncilDecision("Conclusion", votes, [], 0.9, []);

        // Act & Assert
        decision.IsConsensus.Should().BeTrue();
    }

    [Fact]
    public void IsConsensus_WithDifferentPositions_ReturnsFalse()
    {
        // Arrange
        var votes = new Dictionary<string, AgentVote>
        {
            ["Agent1"] = new("Agent1", "APPROVE", 1.0, "R1"),
            ["Agent2"] = new("Agent2", "REJECT", 0.9, "R2")
        };

        var decision = new CouncilDecision("Conclusion", votes, [], 0.5, []);

        // Act & Assert
        decision.IsConsensus.Should().BeFalse();
    }

    [Fact]
    public void IsConsensus_WithNoVotes_ReturnsFalse()
    {
        // Arrange
        var decision = new CouncilDecision(
            "Conclusion",
            new Dictionary<string, AgentVote>(),
            [],
            0.0,
            []);

        // Act & Assert
        decision.IsConsensus.Should().BeFalse();
    }

    [Fact]
    public void IsConsensus_WithSingleVote_ReturnsTrue()
    {
        // Arrange
        var votes = new Dictionary<string, AgentVote>
        {
            ["Agent1"] = new("Agent1", "APPROVE", 1.0, "R1")
        };

        var decision = new CouncilDecision("Conclusion", votes, [], 1.0, []);

        // Act & Assert
        decision.IsConsensus.Should().BeTrue();
    }

    [Fact]
    public void MajorityPosition_ReturnsHighestWeightedPosition()
    {
        // Arrange
        var votes = new Dictionary<string, AgentVote>
        {
            ["Agent1"] = new("Agent1", "APPROVE", 1.0, "R1"),
            ["Agent2"] = new("Agent2", "APPROVE", 0.9, "R2"),
            ["Agent3"] = new("Agent3", "REJECT", 0.5, "R3")
        };

        var decision = new CouncilDecision("Conclusion", votes, [], 0.7, []);

        // Act & Assert
        decision.MajorityPosition.Should().Be("APPROVE");
    }

    [Fact]
    public void MajorityPosition_WithNoVotes_ReturnsNull()
    {
        // Arrange
        var decision = new CouncilDecision(
            "Conclusion",
            new Dictionary<string, AgentVote>(),
            [],
            0.0,
            []);

        // Act & Assert
        decision.MajorityPosition.Should().BeNull();
    }

    [Fact]
    public void MajorityPosition_WithTiedWeights_ReturnsFirstGrouped()
    {
        // Arrange — both positions have equal total weight
        var votes = new Dictionary<string, AgentVote>
        {
            ["Agent1"] = new("Agent1", "APPROVE", 1.0, "R1"),
            ["Agent2"] = new("Agent2", "REJECT", 1.0, "R2")
        };

        var decision = new CouncilDecision("Conclusion", votes, [], 0.5, []);

        // Act — one of the two must be returned
        decision.MajorityPosition.Should().NotBeNull();
    }

    [Fact]
    public void Failed_ReturnsDecisionWithFailureMessage()
    {
        // Act
        var decision = CouncilDecision.Failed("Test failure reason");

        // Assert
        decision.Conclusion.Should().Contain("Deliberation failed");
        decision.Conclusion.Should().Contain("Test failure reason");
        decision.Votes.Should().BeEmpty();
        decision.Transcript.Should().BeEmpty();
        decision.Confidence.Should().Be(0.0);
        decision.MinorityOpinions.Should().BeEmpty();
    }

    [Fact]
    public void Failed_IsConsensus_ReturnsFalse()
    {
        // Arrange
        var decision = CouncilDecision.Failed("reason");

        // Act & Assert
        decision.IsConsensus.Should().BeFalse();
    }

    [Fact]
    public void Failed_MajorityPosition_ReturnsNull()
    {
        // Arrange
        var decision = CouncilDecision.Failed("reason");

        // Act & Assert
        decision.MajorityPosition.Should().BeNull();
    }
}

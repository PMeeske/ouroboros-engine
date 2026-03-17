using Ouroboros.Pipeline.Council;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class AgentVoteTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Act
        var vote = new AgentVote("TestAgent", "APPROVE", 0.85, "Strong proposal");

        // Assert
        vote.AgentName.Should().Be("TestAgent");
        vote.Position.Should().Be("APPROVE");
        vote.Weight.Should().Be(0.85);
        vote.Rationale.Should().Be("Strong proposal");
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Act
        var vote1 = new AgentVote("Agent", "APPROVE", 1.0, "Rationale");
        var vote2 = new AgentVote("Agent", "APPROVE", 1.0, "Rationale");

        // Assert
        vote1.Should().Be(vote2);
    }

    [Fact]
    public void RecordEquality_WithDifferentPosition_AreNotEqual()
    {
        // Act
        var vote1 = new AgentVote("Agent", "APPROVE", 1.0, "Rationale");
        var vote2 = new AgentVote("Agent", "REJECT", 1.0, "Rationale");

        // Assert
        vote1.Should().NotBe(vote2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = new AgentVote("Agent", "APPROVE", 0.9, "Good");

        // Act
        var modified = original with { Weight = 0.5 };

        // Assert
        modified.AgentName.Should().Be("Agent");
        modified.Position.Should().Be("APPROVE");
        modified.Weight.Should().Be(0.5);
        modified.Rationale.Should().Be("Good");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Weight_AcceptsValidRange(double weight)
    {
        // Act
        var vote = new AgentVote("Agent", "APPROVE", weight, "Rationale");

        // Assert
        vote.Weight.Should().Be(weight);
    }
}

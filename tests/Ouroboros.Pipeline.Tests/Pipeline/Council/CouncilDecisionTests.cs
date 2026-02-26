namespace Ouroboros.Tests.Pipeline.Council;

using Ouroboros.Pipeline.Council;

[Trait("Category", "Unit")]
public class CouncilDecisionTests
{
    [Fact]
    public void Failed_CreatesDecisionWithFailureConclusion()
    {
        var decision = CouncilDecision.Failed("timeout");

        decision.Conclusion.Should().Contain("Deliberation failed");
        decision.Conclusion.Should().Contain("timeout");
        decision.Votes.Should().BeEmpty();
        decision.Transcript.Should().BeEmpty();
        decision.Confidence.Should().Be(0.0);
        decision.MinorityOpinions.Should().BeEmpty();
    }

    [Fact]
    public void IsConsensus_WithAllSamePosition_ReturnsTrue()
    {
        var votes = new Dictionary<string, AgentVote>
        {
            ["A"] = new AgentVote("A", "APPROVE", 1.0, "OK"),
            ["B"] = new AgentVote("B", "APPROVE", 1.0, "OK"),
        };

        var decision = new CouncilDecision("Done", votes, [], 0.9, []);

        decision.IsConsensus.Should().BeTrue();
    }

    [Fact]
    public void IsConsensus_WithDifferentPositions_ReturnsFalse()
    {
        var votes = new Dictionary<string, AgentVote>
        {
            ["A"] = new AgentVote("A", "APPROVE", 1.0, "OK"),
            ["B"] = new AgentVote("B", "REJECT", 1.0, "Not OK"),
        };

        var decision = new CouncilDecision("Split", votes, [], 0.5, []);

        decision.IsConsensus.Should().BeFalse();
    }

    [Fact]
    public void MajorityPosition_ReturnsHighestWeightedPosition()
    {
        var votes = new Dictionary<string, AgentVote>
        {
            ["A"] = new AgentVote("A", "APPROVE", 0.9, "OK"),
            ["B"] = new AgentVote("B", "REJECT", 0.3, "Not OK"),
            ["C"] = new AgentVote("C", "APPROVE", 0.8, "Fine"),
        };

        var decision = new CouncilDecision("Result", votes, [], 0.8, []);

        decision.MajorityPosition.Should().Be("APPROVE");
    }
}

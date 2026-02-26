namespace Ouroboros.Tests.Pipeline.Council;

using Ouroboros.Pipeline.Council;

[Trait("Category", "Unit")]
public class AgentVoteTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var vote = new AgentVote("Optimist", "APPROVE", 0.9, "Good proposal");

        vote.AgentName.Should().Be("Optimist");
        vote.Position.Should().Be("APPROVE");
        vote.Weight.Should().Be(0.9);
        vote.Rationale.Should().Be("Good proposal");
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var v1 = new AgentVote("A", "APPROVE", 1.0, "R");
        var v2 = new AgentVote("A", "APPROVE", 1.0, "R");

        v1.Should().Be(v2);
    }
}

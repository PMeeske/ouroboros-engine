namespace Ouroboros.Tests.Pipeline.Council;

using Ouroboros.Pipeline.Council;

[Trait("Category", "Unit")]
public class DebateRoundTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var contributions = new List<AgentContribution>
        {
            new("Agent1", "Content1")
        };
        var now = DateTime.UtcNow;

        var round = new DebateRound(DebatePhase.Proposal, 1, contributions, now);

        round.Phase.Should().Be(DebatePhase.Proposal);
        round.RoundNumber.Should().Be(1);
        round.Contributions.Should().HaveCount(1);
        round.Timestamp.Should().Be(now);
    }
}

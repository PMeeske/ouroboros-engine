namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using AgentVoteMA = Ouroboros.Pipeline.MultiAgent.AgentVote;

[Trait("Category", "Unit")]
public class ConsensusResultTests
{
    [Fact]
    public void NoConsensus_SetsPropertiesCorrectly()
    {
        var votes = new List<AgentVoteMA>
        {
            AgentVoteMA.Create(Guid.NewGuid(), "A", 0.8),
            AgentVoteMA.Create(Guid.NewGuid(), "B", 0.7),
        };

        var result = ConsensusResult.NoConsensus(votes, "majority");

        result.HasConsensus.Should().BeFalse();
        result.WinningOption.Should().BeEmpty();
        result.Protocol.Should().Be("majority");
        result.TotalVotes.Should().Be(2);
    }

    [Fact]
    public void NoConsensus_ComputesVoteCounts()
    {
        var votes = new List<AgentVoteMA>
        {
            AgentVoteMA.Create(Guid.NewGuid(), "A", 0.8),
            AgentVoteMA.Create(Guid.NewGuid(), "A", 0.6),
            AgentVoteMA.Create(Guid.NewGuid(), "B", 0.7),
        };

        var result = ConsensusResult.NoConsensus(votes, "test");

        result.VoteCounts["A"].Should().Be(2);
        result.VoteCounts["B"].Should().Be(1);
    }

    [Fact]
    public void NoConsensus_ComputesConfidenceByOption()
    {
        var votes = new List<AgentVoteMA>
        {
            AgentVoteMA.Create(Guid.NewGuid(), "A", 0.8),
            AgentVoteMA.Create(Guid.NewGuid(), "A", 0.6),
        };

        var result = ConsensusResult.NoConsensus(votes, "test");

        result.ConfidenceByOption["A"].Should().BeApproximately(1.4, 0.001);
    }

    [Fact]
    public void ParticipationRate_ComputesCorrectly()
    {
        var votes = new List<AgentVoteMA>
        {
            AgentVoteMA.Create(Guid.NewGuid(), "A", 0.8),
            AgentVoteMA.Create(Guid.NewGuid(), "B", 0.7),
        };

        var result = ConsensusResult.NoConsensus(votes, "test");

        result.ParticipationRate(4).Should().Be(0.5);
    }

    [Fact]
    public void ParticipationRate_ThrowsOnZeroAgents()
    {
        var result = ConsensusResult.NoConsensus(new List<AgentVoteMA>(), "test");
        var act = () => result.ParticipationRate(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

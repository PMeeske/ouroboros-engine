using FluentAssertions;
using NSubstitute;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class VotingSessionTests
{
    private static IConsensusProtocol CreateMockProtocol()
    {
        var protocol = Substitute.For<IConsensusProtocol>();
        protocol.Strategy.Returns(ConsensusStrategy.Majority);
        protocol.Evaluate(Arg.Any<IReadOnlyList<AgentVote>>())
            .Returns(callInfo =>
            {
                var votes = callInfo.Arg<IReadOnlyList<AgentVote>>();
                return ConsensusResult.NoConsensus(votes, "Majority");
            });
        return protocol;
    }

    [Fact]
    public void Create_WithValidParams_ReturnsSession()
    {
        // Arrange
        var options = new List<string> { "A", "B", "C" };

        // Act
        var session = VotingSession.Create("Test topic", options, CreateMockProtocol());

        // Assert
        session.SessionId.Should().NotBeEmpty();
        session.Topic.Should().Be("Test topic");
        session.Options.Should().BeEquivalentTo(new[] { "A", "B", "C" });
        session.VoteCount.Should().Be(0);
    }

    [Fact]
    public void Create_WithNullTopic_ThrowsArgumentNullException()
    {
        Action act = () => VotingSession.Create(null!, new List<string> { "A" }, CreateMockProtocol());
        act.Should().Throw<ArgumentNullException>().WithParameterName("topic");
    }

    [Fact]
    public void Create_WithNullOptions_ThrowsArgumentNullException()
    {
        Action act = () => VotingSession.Create("topic", null!, CreateMockProtocol());
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Create_WithNullProtocol_ThrowsArgumentNullException()
    {
        Action act = () => VotingSession.Create("topic", new List<string> { "A" }, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("protocol");
    }

    [Fact]
    public void Create_WithEmptyOptions_ThrowsArgumentException()
    {
        Action act = () => VotingSession.Create("topic", new List<string>(), CreateMockProtocol());
        act.Should().Throw<ArgumentException>().WithParameterName("options");
    }

    [Fact]
    public void CastVote_WithValidVote_IncrementsVoteCount()
    {
        // Arrange
        var session = VotingSession.Create("topic", new List<string> { "A", "B" }, CreateMockProtocol());
        var vote = AgentVote.Create(Guid.NewGuid(), "A", 0.8);

        // Act
        session.CastVote(vote);

        // Assert
        session.VoteCount.Should().Be(1);
    }

    [Fact]
    public void CastVote_WithNullVote_ThrowsArgumentNullException()
    {
        var session = VotingSession.Create("topic", new List<string> { "A" }, CreateMockProtocol());
        Action act = () => session.CastVote(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("vote");
    }

    [Fact]
    public void CastVote_WhenAgentAlreadyVoted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = VotingSession.Create("topic", new List<string> { "A", "B" }, CreateMockProtocol());
        var agentId = Guid.NewGuid();
        session.CastVote(AgentVote.Create(agentId, "A", 0.8));

        // Act
        Action act = () => session.CastVote(AgentVote.Create(agentId, "B", 0.9));

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CastVote_WithInvalidOption_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = VotingSession.Create("topic", new List<string> { "A", "B" }, CreateMockProtocol());

        // Act
        Action act = () => session.CastVote(AgentVote.Create(Guid.NewGuid(), "C", 0.8));

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HasVoted_WhenAgentHasVoted_ReturnsTrue()
    {
        // Arrange
        var session = VotingSession.Create("topic", new List<string> { "A" }, CreateMockProtocol());
        var agentId = Guid.NewGuid();
        session.CastVote(AgentVote.Create(agentId, "A", 0.8));

        // Act & Assert
        session.HasVoted(agentId).Should().BeTrue();
    }

    [Fact]
    public void HasVoted_WhenAgentHasNotVoted_ReturnsFalse()
    {
        var session = VotingSession.Create("topic", new List<string> { "A" }, CreateMockProtocol());
        session.HasVoted(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void GetVotes_ReturnsAllCastVotes()
    {
        // Arrange
        var session = VotingSession.Create("topic", new List<string> { "A", "B" }, CreateMockProtocol());
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "A", 0.8));
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "B", 0.7));

        // Act
        var votes = session.GetVotes();

        // Assert
        votes.Should().HaveCount(2);
    }

    [Fact]
    public void GetResult_DelegatesToProtocol()
    {
        // Arrange
        var protocol = CreateMockProtocol();
        var session = VotingSession.Create("topic", new List<string> { "A" }, protocol);
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "A", 0.9));

        // Act
        var result = session.GetResult();

        // Assert
        protocol.Received(1).Evaluate(Arg.Any<IReadOnlyList<AgentVote>>());
    }

    [Fact]
    public void TryGetResult_WhenConsensusReached_ReturnsSome()
    {
        // Arrange
        var protocol = Substitute.For<IConsensusProtocol>();
        protocol.Evaluate(Arg.Any<IReadOnlyList<AgentVote>>())
            .Returns(new ConsensusResult(
                "A", 0.9,
                new List<AgentVote>(),
                new Dictionary<string, int> { { "A", 1 } },
                new Dictionary<string, double> { { "A", 0.9 } },
                true, "Majority"));

        var session = VotingSession.Create("topic", new List<string> { "A" }, protocol);
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "A", 0.9));

        // Act
        var result = session.TryGetResult();

        // Assert
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void TryGetResult_WhenNoConsensus_ReturnsNone()
    {
        // Arrange
        var session = VotingSession.Create("topic", new List<string> { "A", "B" }, CreateMockProtocol());

        // Act
        var result = session.TryGetResult();

        // Assert
        result.HasValue.Should().BeFalse();
    }
}

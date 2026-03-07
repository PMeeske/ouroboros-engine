// <copyright file="VotingSessionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public sealed class VotingSessionTests
{
    private static VotingSession CreateSession(params string[] options)
        => VotingSession.Create("test-topic", options, ConsensusProtocol.Majority);

    // --- Create ---

    [Fact]
    public void Create_NullTopic_Throws()
    {
        Action act = () => VotingSession.Create(null!, new[] { "A" }, ConsensusProtocol.Majority);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_NullOptions_Throws()
    {
        Action act = () => VotingSession.Create("topic", null!, ConsensusProtocol.Majority);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_EmptyOptions_Throws()
    {
        Action act = () => VotingSession.Create("topic", Array.Empty<string>(), ConsensusProtocol.Majority);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NullProtocol_Throws()
    {
        Action act = () => VotingSession.Create("topic", new[] { "A" }, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ValidInput_SetsProperties()
    {
        var session = CreateSession("A", "B");

        session.Topic.Should().Be("test-topic");
        session.Options.Should().ContainInOrder("A", "B");
        session.SessionId.Should().NotBe(Guid.Empty);
        session.VoteCount.Should().Be(0);
    }

    // --- CastVote ---

    [Fact]
    public void CastVote_NullVote_Throws()
    {
        var session = CreateSession("A", "B");
        Action act = () => session.CastVote(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CastVote_ValidVote_IncrementsCount()
    {
        var session = CreateSession("A", "B");
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "A", 0.8));

        session.VoteCount.Should().Be(1);
    }

    [Fact]
    public void CastVote_DuplicateAgent_Throws()
    {
        var session = CreateSession("A", "B");
        var agentId = Guid.NewGuid();

        session.CastVote(AgentVote.Create(agentId, "A", 0.8));

        Action act = () => session.CastVote(AgentVote.Create(agentId, "B", 0.7));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already voted*");
    }

    [Fact]
    public void CastVote_InvalidOption_Throws()
    {
        var session = CreateSession("A", "B");

        Action act = () => session.CastVote(AgentVote.Create(Guid.NewGuid(), "C", 0.8));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a valid option*");
    }

    // --- HasVoted ---

    [Fact]
    public void HasVoted_BeforeVoting_ReturnsFalse()
    {
        var session = CreateSession("A");
        session.HasVoted(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void HasVoted_AfterVoting_ReturnsTrue()
    {
        var session = CreateSession("A");
        var agentId = Guid.NewGuid();

        session.CastVote(AgentVote.Create(agentId, "A", 0.8));

        session.HasVoted(agentId).Should().BeTrue();
    }

    // --- GetResult ---

    [Fact]
    public void GetResult_NoVotes_NoConsensus()
    {
        var session = CreateSession("A", "B");
        var result = session.GetResult();

        result.HasConsensus.Should().BeFalse();
    }

    [Fact]
    public void GetResult_MajorityVotes_HasConsensus()
    {
        var session = CreateSession("A", "B");
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "A", 0.8));
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "A", 0.9));
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "B", 0.5));

        var result = session.GetResult();

        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("A");
    }

    // --- TryGetResult ---

    [Fact]
    public void TryGetResult_NoConsensus_ReturnsNone()
    {
        var session = CreateSession("A", "B");
        var result = session.TryGetResult();

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void TryGetResult_WithConsensus_ReturnsSome()
    {
        var session = CreateSession("A", "B");
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "A", 0.8));
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "A", 0.9));
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "B", 0.5));

        var result = session.TryGetResult();

        result.HasValue.Should().BeTrue();
    }

    // --- GetVotes ---

    [Fact]
    public void GetVotes_ReturnsImmutableSnapshot()
    {
        var session = CreateSession("A");
        var agentId = Guid.NewGuid();
        session.CastVote(AgentVote.Create(agentId, "A", 0.8));

        var votes = session.GetVotes();

        votes.Should().HaveCount(1);
        votes[0].AgentId.Should().Be(agentId);
    }
}

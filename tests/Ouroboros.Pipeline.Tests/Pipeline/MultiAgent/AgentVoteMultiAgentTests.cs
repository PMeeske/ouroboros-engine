namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using AgentVoteMA = Ouroboros.Pipeline.MultiAgent.AgentVote;

[Trait("Category", "Unit")]
public class AgentVoteMultiAgentTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var agentId = Guid.NewGuid();
        var vote = AgentVoteMA.Create(agentId, "optionA", 0.9, "strong preference");

        vote.AgentId.Should().Be(agentId);
        vote.Option.Should().Be("optionA");
        vote.Confidence.Should().Be(0.9);
        vote.Reasoning.Should().Be("strong preference");
    }

    [Fact]
    public void Create_ThrowsOnNullOption()
    {
        var act = () => AgentVoteMA.Create(Guid.NewGuid(), null!, 0.5);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ThrowsOnInvalidConfidence()
    {
        var act = () => AgentVoteMA.Create(Guid.NewGuid(), "opt", -0.1);
        act.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => AgentVoteMA.Create(Guid.NewGuid(), "opt", 1.1);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_AcceptsNullReasoning()
    {
        var vote = AgentVoteMA.Create(Guid.NewGuid(), "opt", 0.5);
        vote.Reasoning.Should().BeNull();
    }
}

namespace Ouroboros.Tests.Pipeline.Council.Agents;

using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Council.Agents;

[Trait("Category", "Unit")]
public class AgentPersonaArrowsTests
{
    [Fact]
    public void BuildDefaultProposalPrompt_ContainsTopicAndSystemPrompt()
    {
        var topic = CouncilTopic.Simple("Should we refactor?");
        var prompt = AgentPersonaArrows.BuildDefaultProposalPrompt(topic, "You are a tester.");

        prompt.Should().Contain("Should we refactor?");
        prompt.Should().Contain("You are a tester.");
    }

    [Fact]
    public void BuildDefaultChallengePrompt_ContainsOtherProposals()
    {
        var topic = CouncilTopic.Simple("Q?");
        var proposals = new List<AgentContribution>
        {
            new("Agent1", "Proposal content")
        };

        var prompt = AgentPersonaArrows.BuildDefaultChallengePrompt(topic, proposals, "System");

        prompt.Should().Contain("Agent1");
        prompt.Should().Contain("Proposal content");
    }

    [Fact]
    public void BuildDefaultRefinementPrompt_ContainsOwnProposalAndChallenges()
    {
        var topic = CouncilTopic.Simple("Q?");
        var challenges = new List<AgentContribution> { new("Critic", "Flaw found") };
        var ownProposal = new AgentContribution("Me", "My proposal");

        var prompt = AgentPersonaArrows.BuildDefaultRefinementPrompt(topic, challenges, ownProposal, "Sys");

        prompt.Should().Contain("My proposal");
        prompt.Should().Contain("Flaw found");
    }

    [Fact]
    public void BuildDefaultVotePrompt_ContainsTranscript()
    {
        var topic = CouncilTopic.Simple("Q?");
        var transcript = new List<DebateRound>
        {
            new(DebatePhase.Proposal, 1, new List<AgentContribution> { new("A", "Content") }, DateTime.UtcNow)
        };

        var prompt = AgentPersonaArrows.BuildDefaultVotePrompt(topic, transcript, "Sys");

        prompt.Should().Contain("Proposal");
        prompt.Should().Contain("APPROVE");
    }

    [Fact]
    public void ParseDefaultVoteResponse_ParsesPositionAndRationale()
    {
        var response = "POSITION: APPROVE\nRATIONALE: Good idea";
        var vote = AgentPersonaArrows.ParseDefaultVoteResponse("TestAgent", response, 0.8);

        vote.AgentName.Should().Be("TestAgent");
        vote.Position.Should().Be("APPROVE");
        vote.Rationale.Should().Be("Good idea");
        vote.Weight.Should().Be(0.8);
    }

    [Fact]
    public void ParseDefaultVoteResponse_DefaultsToAbstainWhenNoPositionLine()
    {
        var response = "Some random text with no structured format.";
        var vote = AgentPersonaArrows.ParseDefaultVoteResponse("TestAgent", response, 0.5);

        vote.Position.Should().Be("ABSTAIN");
    }
}

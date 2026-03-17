using NSubstitute;
using Ouroboros.Abstractions.Core;
using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Council.Agents;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class AgentPersonaArrowsTests
{
    private static ToolAwareChatModel CreateMockLlm(string response = "Test response")
    {
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    private static ToolAwareChatModel CreateFailingLlm(string errorMessage = "LLM failure")
    {
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidOperationException(errorMessage));
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    #region CreateProposalArrow Tests

    [Fact]
    public async Task CreateProposalArrow_WithSuccessfulLlm_ReturnsSuccess()
    {
        // Arrange
        var llm = CreateMockLlm("My proposal content");
        var topic = CouncilTopic.Simple("Test question");
        var arrow = AgentPersonaArrows.CreateProposalArrow(
            "TestAgent", "System prompt",
            AgentPersonaArrows.BuildDefaultProposalPrompt, llm);

        // Act
        var result = await arrow(topic);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgentName.Should().Be("TestAgent");
        result.Value.Content.Should().Be("My proposal content");
    }

    [Fact]
    public async Task CreateProposalArrow_WithFailingLlm_ReturnsFailure()
    {
        // Arrange
        var llm = CreateFailingLlm("Connection timeout");
        var topic = CouncilTopic.Simple("Test question");
        var arrow = AgentPersonaArrows.CreateProposalArrow(
            "TestAgent", "System prompt",
            AgentPersonaArrows.BuildDefaultProposalPrompt, llm);

        // Act
        var result = await arrow(topic);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("TestAgent");
        result.Error.Should().Contain("Proposal generation failed");
        result.Error.Should().Contain("Connection timeout");
    }

    [Fact]
    public async Task CreateProposalArrow_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new OperationCanceledException());
        var llm = new ToolAwareChatModel(mockModel, new ToolRegistry());
        var topic = CouncilTopic.Simple("Test");
        var arrow = AgentPersonaArrows.CreateProposalArrow(
            "TestAgent", "Prompt",
            AgentPersonaArrows.BuildDefaultProposalPrompt, llm);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => arrow(topic));
    }

    #endregion

    #region CreateChallengeArrow Tests

    [Fact]
    public async Task CreateChallengeArrow_WithSuccessfulLlm_ReturnsSuccess()
    {
        // Arrange
        var llm = CreateMockLlm("My challenge content");
        var topic = CouncilTopic.Simple("Test question");
        var proposals = new List<AgentContribution>
        {
            new("Agent1", "Proposal 1")
        };
        var arrow = AgentPersonaArrows.CreateChallengeArrow(
            "Challenger", "System prompt",
            AgentPersonaArrows.BuildDefaultChallengePrompt, llm);

        // Act
        var result = await arrow((topic, proposals));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgentName.Should().Be("Challenger");
        result.Value.Content.Should().Be("My challenge content");
    }

    [Fact]
    public async Task CreateChallengeArrow_WithFailingLlm_ReturnsFailure()
    {
        // Arrange
        var llm = CreateFailingLlm("Timeout");
        var topic = CouncilTopic.Simple("Test");
        var proposals = new List<AgentContribution>();
        var arrow = AgentPersonaArrows.CreateChallengeArrow(
            "Challenger", "Prompt",
            AgentPersonaArrows.BuildDefaultChallengePrompt, llm);

        // Act
        var result = await arrow((topic, proposals));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Challenger");
        result.Error.Should().Contain("Challenge generation failed");
    }

    #endregion

    #region CreateRefinementArrow Tests

    [Fact]
    public async Task CreateRefinementArrow_WithSuccessfulLlm_ReturnsSuccess()
    {
        // Arrange
        var llm = CreateMockLlm("Refined position");
        var topic = CouncilTopic.Simple("Test question");
        var challenges = new List<AgentContribution> { new("Critic", "Critique") };
        var ownProposal = new AgentContribution("Refiner", "Original proposal");
        var arrow = AgentPersonaArrows.CreateRefinementArrow(
            "Refiner", "System prompt",
            AgentPersonaArrows.BuildDefaultRefinementPrompt, llm);

        // Act
        var result = await arrow((topic, challenges, ownProposal));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgentName.Should().Be("Refiner");
        result.Value.Content.Should().Be("Refined position");
    }

    [Fact]
    public async Task CreateRefinementArrow_WithFailingLlm_ReturnsFailure()
    {
        // Arrange
        var llm = CreateFailingLlm();
        var topic = CouncilTopic.Simple("Test");
        var challenges = new List<AgentContribution>();
        var ownProposal = new AgentContribution("Refiner", "Proposal");
        var arrow = AgentPersonaArrows.CreateRefinementArrow(
            "Refiner", "Prompt",
            AgentPersonaArrows.BuildDefaultRefinementPrompt, llm);

        // Act
        var result = await arrow((topic, challenges, ownProposal));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Refiner");
        result.Error.Should().Contain("Refinement generation failed");
    }

    #endregion

    #region CreateVoteArrow Tests

    [Fact]
    public async Task CreateVoteArrow_WithApproveResponse_ReturnsApproveVote()
    {
        // Arrange
        var llm = CreateMockLlm("POSITION: APPROVE\nRATIONALE: Sound reasoning");
        var topic = CouncilTopic.Simple("Test question");
        var transcript = new List<DebateRound>();
        var arrow = AgentPersonaArrows.CreateVoteArrow(
            "Voter", "System prompt", 0.9,
            AgentPersonaArrows.BuildDefaultVotePrompt,
            AgentPersonaArrows.ParseDefaultVoteResponse, llm);

        // Act
        var result = await arrow((topic, transcript));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgentName.Should().Be("Voter");
        result.Value.Position.Should().Be("APPROVE");
        result.Value.Weight.Should().Be(0.9);
        result.Value.Rationale.Should().Contain("Sound reasoning");
    }

    [Fact]
    public async Task CreateVoteArrow_WithRejectResponse_ReturnsRejectVote()
    {
        // Arrange
        var llm = CreateMockLlm("POSITION: REJECT\nRATIONALE: Too risky");
        var topic = CouncilTopic.Simple("Test question");
        var transcript = new List<DebateRound>();
        var arrow = AgentPersonaArrows.CreateVoteArrow(
            "Voter", "System prompt", 0.8,
            AgentPersonaArrows.BuildDefaultVotePrompt,
            AgentPersonaArrows.ParseDefaultVoteResponse, llm);

        // Act
        var result = await arrow((topic, transcript));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Position.Should().Be("REJECT");
    }

    [Fact]
    public async Task CreateVoteArrow_WithFailingLlm_ReturnsFailure()
    {
        // Arrange
        var llm = CreateFailingLlm();
        var topic = CouncilTopic.Simple("Test");
        var transcript = new List<DebateRound>();
        var arrow = AgentPersonaArrows.CreateVoteArrow(
            "Voter", "Prompt", 0.9,
            AgentPersonaArrows.BuildDefaultVotePrompt,
            AgentPersonaArrows.ParseDefaultVoteResponse, llm);

        // Act
        var result = await arrow((topic, transcript));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Voter");
        result.Error.Should().Contain("Vote generation failed");
    }

    #endregion

    #region BuildDefaultProposalPrompt Tests

    [Fact]
    public void BuildDefaultProposalPrompt_ContainsTopicQuestion()
    {
        // Arrange
        var topic = CouncilTopic.Simple("Should we use microservices?");

        // Act
        var prompt = AgentPersonaArrows.BuildDefaultProposalPrompt(topic, "You are a helpful agent.");

        // Assert
        prompt.Should().Contain("Should we use microservices?");
        prompt.Should().Contain("You are a helpful agent.");
    }

    [Fact]
    public void BuildDefaultProposalPrompt_ContainsBackgroundAndConstraints()
    {
        // Arrange
        var topic = new CouncilTopic(
            "Should we refactor?",
            "System is legacy",
            new List<string> { "Must maintain uptime", "Budget limited" });

        // Act
        var prompt = AgentPersonaArrows.BuildDefaultProposalPrompt(topic, "System prompt");

        // Assert
        prompt.Should().Contain("System is legacy");
        prompt.Should().Contain("Must maintain uptime");
        prompt.Should().Contain("Budget limited");
    }

    #endregion

    #region BuildDefaultChallengePrompt Tests

    [Fact]
    public void BuildDefaultChallengePrompt_ContainsOtherProposals()
    {
        // Arrange
        var topic = CouncilTopic.Simple("Question");
        var proposals = new List<AgentContribution>
        {
            new("Optimist", "Great idea!"),
            new("Pragmatist", "Consider the costs.")
        };

        // Act
        var prompt = AgentPersonaArrows.BuildDefaultChallengePrompt(topic, proposals, "System prompt");

        // Assert
        prompt.Should().Contain("Optimist");
        prompt.Should().Contain("Great idea!");
        prompt.Should().Contain("Pragmatist");
        prompt.Should().Contain("Consider the costs.");
    }

    #endregion

    #region BuildDefaultRefinementPrompt Tests

    [Fact]
    public void BuildDefaultRefinementPrompt_ContainsChallengesAndOwnProposal()
    {
        // Arrange
        var topic = CouncilTopic.Simple("Question");
        var challenges = new List<AgentContribution>
        {
            new("Critic", "This has flaws")
        };
        var ownProposal = new AgentContribution("Agent", "My original position");

        // Act
        var prompt = AgentPersonaArrows.BuildDefaultRefinementPrompt(
            topic, challenges, ownProposal, "System prompt");

        // Assert
        prompt.Should().Contain("My original position");
        prompt.Should().Contain("This has flaws");
        prompt.Should().Contain("Critic");
    }

    #endregion

    #region BuildDefaultVotePrompt Tests

    [Fact]
    public void BuildDefaultVotePrompt_ContainsTranscript()
    {
        // Arrange
        var topic = CouncilTopic.Simple("Question");
        var transcript = new List<DebateRound>
        {
            new(DebatePhase.Proposal, 1,
                new List<AgentContribution> { new("Agent1", "Proposal content") },
                DateTime.UtcNow)
        };

        // Act
        var prompt = AgentPersonaArrows.BuildDefaultVotePrompt(topic, transcript, "System prompt");

        // Assert
        prompt.Should().Contain("Agent1");
        prompt.Should().Contain("Proposal content");
        prompt.Should().Contain("APPROVE/REJECT/ABSTAIN");
    }

    #endregion

    #region ParseDefaultVoteResponse Tests

    [Fact]
    public void ParseDefaultVoteResponse_WithValidFormat_ParsesCorrectly()
    {
        // Act
        var vote = AgentPersonaArrows.ParseDefaultVoteResponse(
            "TestAgent",
            "POSITION: APPROVE\nRATIONALE: Good proposal",
            0.85);

        // Assert
        vote.AgentName.Should().Be("TestAgent");
        vote.Position.Should().Be("APPROVE");
        vote.Weight.Should().Be(0.85);
        vote.Rationale.Should().Be("Good proposal");
    }

    [Fact]
    public void ParseDefaultVoteResponse_WithRejectPosition_ParsesCorrectly()
    {
        // Act
        var vote = AgentPersonaArrows.ParseDefaultVoteResponse(
            "Agent",
            "POSITION: REJECT\nRATIONALE: Too risky",
            1.0);

        // Assert
        vote.Position.Should().Be("REJECT");
        vote.Rationale.Should().Be("Too risky");
    }

    [Fact]
    public void ParseDefaultVoteResponse_WithAbstainPosition_ParsesCorrectly()
    {
        // Act
        var vote = AgentPersonaArrows.ParseDefaultVoteResponse(
            "Agent",
            "POSITION: ABSTAIN\nRATIONALE: Need more information",
            0.5);

        // Assert
        vote.Position.Should().Be("ABSTAIN");
    }

    [Fact]
    public void ParseDefaultVoteResponse_WithNoPositionLine_DefaultsToAbstain()
    {
        // Act
        var vote = AgentPersonaArrows.ParseDefaultVoteResponse(
            "Agent",
            "I think this is complicated and I cannot decide.",
            0.7);

        // Assert
        vote.Position.Should().Be("ABSTAIN");
    }

    [Fact]
    public void ParseDefaultVoteResponse_WithNoRationaleLine_UsesFullResponseAsRationale()
    {
        // Arrange
        var response = "POSITION: APPROVE\nSome extra text without RATIONALE prefix";

        // Act
        var vote = AgentPersonaArrows.ParseDefaultVoteResponse("Agent", response, 0.9);

        // Assert
        vote.Position.Should().Be("APPROVE");
        vote.Rationale.Should().Be(response);
    }

    [Fact]
    public void ParseDefaultVoteResponse_CaseInsensitivePositionParsing()
    {
        // Act
        var vote = AgentPersonaArrows.ParseDefaultVoteResponse(
            "Agent",
            "position: approve\nrationale: Looks good",
            0.8);

        // Assert
        vote.Position.Should().Be("approve");
        vote.Rationale.Should().Be("Looks good");
    }

    [Fact]
    public void ParseDefaultVoteResponse_SetsAgentNameAndWeight()
    {
        // Act
        var vote = AgentPersonaArrows.ParseDefaultVoteResponse("MyAgent", "Some response", 0.42);

        // Assert
        vote.AgentName.Should().Be("MyAgent");
        vote.Weight.Should().Be(0.42);
    }

    #endregion
}

using NSubstitute;
using Ouroboros.Abstractions.Core;
using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Council.Agents;
using LangChain.Databases;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class CouncilOrchestratorArrowsPipelineTests
{
    private static ToolAwareChatModel CreateMockLlm(string response = "POSITION: APPROVE\nRATIONALE: Good proposal.")
    {
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    private static ToolAwareChatModel CreateFailingLlm()
    {
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidOperationException("LLM error"));
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    private static PipelineBranch CreateTestBranch()
    {
        var store = Substitute.For<IVectorStore>();
        var source = DataSource.FromPath("/test");
        return new PipelineBranch("test-branch", store, source);
    }

    private static IReadOnlyList<IAgentPersona> CreateDefaultAgents()
    {
        return new List<IAgentPersona>
        {
            new OptimistAgent(),
            new SecurityCynicAgent(),
            new PragmatistAgent(),
            new TheoristAgent(),
            new UserAdvocateAgent()
        };
    }

    #region ConveneCouncilArrow Tests

    [Fact]
    public async Task ConveneCouncilArrow_WithNoAgents_AddsFailedDecisionEvent()
    {
        // Arrange
        var llm = CreateMockLlm();
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilOrchestratorArrows.ConveneCouncilArrow(
            llm, new List<IAgentPersona>(), topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().HaveCount(1);
        var evt = result.Events[0].Should().BeOfType<CouncilDecisionEvent>().Subject;
        evt.Decision.Conclusion.Should().Contain("No agents");
    }

    [Fact]
    public async Task ConveneCouncilArrow_WithAgents_AddsDecisionEvent()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test question");
        var branch = CreateTestBranch();
        var arrow = CouncilOrchestratorArrows.ConveneCouncilArrow(llm, agents, topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().NotBeEmpty();
        result.Events.OfType<CouncilDecisionEvent>().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConveneCouncilArrow_PreservesBranch()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilOrchestratorArrows.ConveneCouncilArrow(llm, agents, topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Name.Should().Be("test-branch");
    }

    [Fact]
    public async Task ConveneCouncilArrow_WithNullConfig_UsesDefault()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilOrchestratorArrows.ConveneCouncilArrow(llm, agents, topic, null);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConveneCouncilArrow_WhenAllAgentsFail_AddsFailedDecisionEvent()
    {
        // Arrange
        var llm = CreateFailingLlm();
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilOrchestratorArrows.ConveneCouncilArrow(llm, agents, topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().NotBeEmpty();
        var evt = result.Events.OfType<CouncilDecisionEvent>().First();
        evt.Decision.Conclusion.Should().Contain("failed");
    }

    #endregion

    #region ConveneCouncilWithDefaultAgentsArrow Tests

    [Fact]
    public async Task ConveneCouncilWithDefaultAgentsArrow_CreatesFiveAgents()
    {
        // Arrange
        var llm = CreateMockLlm();
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilOrchestratorArrows.ConveneCouncilWithDefaultAgentsArrow(llm, topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().NotBeEmpty();
    }

    #endregion

    #region SafeConveneCouncilArrow Tests

    [Fact]
    public async Task SafeConveneCouncilArrow_WithNoAgents_ReturnsFailure()
    {
        // Arrange
        var llm = CreateMockLlm();
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilOrchestratorArrows.SafeConveneCouncilArrow(
            llm, new List<IAgentPersona>(), topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No agents");
    }

    [Fact]
    public async Task SafeConveneCouncilArrow_WithAgents_ReturnsSuccess()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilOrchestratorArrows.SafeConveneCouncilArrow(llm, agents, topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Events.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SafeConveneCouncilArrow_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new OperationCanceledException());
        var llm = new ToolAwareChatModel(mockModel, new ToolRegistry());
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilOrchestratorArrows.SafeConveneCouncilArrow(llm, agents, topic);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => arrow(branch));
    }

    #endregion

    #region DynamicConveneCouncilArrow Tests

    [Fact]
    public async Task DynamicConveneCouncilArrow_BuildsTopicFromBranch()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = CreateDefaultAgents();
        var branch = CreateTestBranch();
        CouncilTopic? capturedTopic = null;
        var arrow = CouncilOrchestratorArrows.DynamicConveneCouncilArrow(
            llm, agents,
            b =>
            {
                capturedTopic = CouncilTopic.Simple($"About {b.Name}");
                return capturedTopic;
            });

        // Act
        var result = await arrow(branch);

        // Assert
        capturedTopic.Should().NotBeNull();
        capturedTopic!.Question.Should().Contain("test-branch");
        result.Events.Should().NotBeEmpty();
    }

    #endregion

    #region CreateConfiguredCouncil Tests

    [Fact]
    public async Task CreateConfiguredCouncil_ReturnsFactoryFunction()
    {
        // Arrange
        var llm = CreateMockLlm();
        var factory = CouncilOrchestratorArrows.CreateConfiguredCouncil(llm);
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();

        // Act
        var arrow = factory(topic);
        var result = await arrow(branch);

        // Assert
        result.Events.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateConfiguredCouncil_ReturnsDifferentArrowsPerTopic()
    {
        // Arrange
        var llm = CreateMockLlm();
        var factory = CouncilOrchestratorArrows.CreateConfiguredCouncil(llm);

        // Act
        var arrow1 = factory(CouncilTopic.Simple("Question 1"));
        var arrow2 = factory(CouncilTopic.Simple("Question 2"));

        // Assert — they should be different delegates
        arrow1.Should().NotBeSameAs(arrow2);
    }

    #endregion

    #region ExecuteProposalPhaseAsync Tests

    [Fact]
    public async Task ExecuteProposalPhaseAsync_WithSuccessfulAgents_ReturnsProposalRound()
    {
        // Arrange
        var llm = CreateMockLlm("My proposal");
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var config = CouncilConfig.Default;

        // Act
        var result = await CouncilOrchestratorArrows.ExecuteProposalPhaseAsync(
            llm, agents, topic, config, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Phase.Should().Be(DebatePhase.Proposal);
        result.Value.RoundNumber.Should().Be(1);
        result.Value.Contributions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteProposalPhaseAsync_WhenAllAgentsFail_ReturnsFailure()
    {
        // Arrange
        var llm = CreateFailingLlm();
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var config = CouncilConfig.Default;

        // Act
        var result = await CouncilOrchestratorArrows.ExecuteProposalPhaseAsync(
            llm, agents, topic, config, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No agents were able to generate proposals");
    }

    #endregion

    #region ExecuteChallengePhaseAsync Tests

    [Fact]
    public async Task ExecuteChallengePhaseAsync_WithProposals_ReturnsChallengeRound()
    {
        // Arrange
        var llm = CreateMockLlm("My challenge");
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var proposals = new Dictionary<string, AgentContribution>
        {
            ["Optimist"] = new("Optimist", "Great idea"),
            ["Pragmatist"] = new("Pragmatist", "Consider costs")
        };
        var config = CouncilConfig.Default;

        // Act
        var result = await CouncilOrchestratorArrows.ExecuteChallengePhaseAsync(
            llm, agents, topic, proposals, config, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Phase.Should().Be(DebatePhase.Challenge);
    }

    [Fact]
    public async Task ExecuteChallengePhaseAsync_WhenAllFail_ReturnsFailure()
    {
        // Arrange
        var llm = CreateFailingLlm();
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var proposals = new Dictionary<string, AgentContribution>();
        var config = CouncilConfig.Default;

        // Act
        var result = await CouncilOrchestratorArrows.ExecuteChallengePhaseAsync(
            llm, agents, topic, proposals, config, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No contributions");
    }

    #endregion

    #region ExecuteRefinementPhaseAsync Tests

    [Fact]
    public async Task ExecuteRefinementPhaseAsync_WithProposalsAndChallenges_ReturnsRefinementRound()
    {
        // Arrange
        var llm = CreateMockLlm("Refined position");
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var proposals = new Dictionary<string, AgentContribution>();
        foreach (var agent in agents)
        {
            proposals[agent.Name] = new AgentContribution(agent.Name, "Original proposal");
        }
        var challenges = new List<AgentContribution>
        {
            new("Critic", "Challenge content")
        };
        var config = CouncilConfig.Default;

        // Act
        var result = await CouncilOrchestratorArrows.ExecuteRefinementPhaseAsync(
            llm, agents, topic, proposals, challenges, config, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Phase.Should().Be(DebatePhase.Refinement);
    }

    [Fact]
    public async Task ExecuteRefinementPhaseAsync_AgentsWithoutProposals_AreSkipped()
    {
        // Arrange
        var llm = CreateMockLlm("Refined position");
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        // Only include proposals for first 2 agents
        var proposals = new Dictionary<string, AgentContribution>
        {
            [agents[0].Name] = new(agents[0].Name, "Proposal 1"),
            [agents[1].Name] = new(agents[1].Name, "Proposal 2")
        };
        var challenges = new List<AgentContribution>();
        var config = CouncilConfig.Default;

        // Act
        var result = await CouncilOrchestratorArrows.ExecuteRefinementPhaseAsync(
            llm, agents, topic, proposals, challenges, config, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contributions.Should().HaveCount(2);
    }

    #endregion

    #region ExecuteVotingPhaseAsync Tests

    [Fact]
    public async Task ExecuteVotingPhaseAsync_WithSuccessfulAgents_ReturnsVotes()
    {
        // Arrange
        var llm = CreateMockLlm("POSITION: APPROVE\nRATIONALE: Sound approach");
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var transcript = new List<DebateRound>();
        var config = CouncilConfig.Default;

        // Act
        var result = await CouncilOrchestratorArrows.ExecuteVotingPhaseAsync(
            llm, agents, topic, transcript, config, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var (votes, round) = result.Value;
        votes.Should().NotBeEmpty();
        round.Phase.Should().Be(DebatePhase.Voting);
    }

    [Fact]
    public async Task ExecuteVotingPhaseAsync_VoteContributionsContainPositionAndRationale()
    {
        // Arrange
        var llm = CreateMockLlm("POSITION: APPROVE\nRATIONALE: Good");
        var agents = CreateDefaultAgents();
        var topic = CouncilTopic.Simple("Test");
        var transcript = new List<DebateRound>();
        var config = CouncilConfig.Default;

        // Act
        var result = await CouncilOrchestratorArrows.ExecuteVotingPhaseAsync(
            llm, agents, topic, transcript, config, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var (_, round) = result.Value;
        round.Contributions.Should().NotBeEmpty();
        foreach (var contribution in round.Contributions)
        {
            contribution.Content.Should().Contain("VOTE:");
        }
    }

    #endregion

    #region BuildSynthesisPrompt Tests

    [Fact]
    public void BuildSynthesisPrompt_ContainsTopicAndVoteSummary()
    {
        // Arrange
        var topic = CouncilTopic.Simple("Should we adopt microservices?");
        var transcript = new List<DebateRound>
        {
            new(DebatePhase.Proposal, 1,
                new List<AgentContribution> { new("Agent1", "Proposal content here") },
                DateTime.UtcNow)
        };
        var votes = new Dictionary<string, AgentVote>
        {
            ["Agent1"] = new("Agent1", "APPROVE", 0.9, "Good idea")
        };

        // Act
        var prompt = CouncilOrchestratorArrows.BuildSynthesisPrompt(
            topic, transcript, votes, "APPROVE", true);

        // Assert
        prompt.Should().NotBeNullOrWhiteSpace();
    }

    #endregion
}

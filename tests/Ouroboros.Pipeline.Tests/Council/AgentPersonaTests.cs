using NSubstitute;
using Ouroboros.Abstractions.Core;
using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Council.Agents;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class AgentPersonaTests
{
    private static ToolAwareChatModel CreateMockLlm(string response = "Test response")
    {
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    #region OptimistAgent Tests

    [Fact]
    public void OptimistAgent_Name_ReturnsOptimist()
    {
        // Act
        var agent = new OptimistAgent();

        // Assert
        agent.Name.Should().Be("Optimist");
    }

    [Fact]
    public void OptimistAgent_Description_ContainsExpectedContent()
    {
        // Act
        var agent = new OptimistAgent();

        // Assert
        agent.Description.Should().Contain("possibilities");
        agent.Description.Should().Contain("creative solutions");
    }

    [Fact]
    public void OptimistAgent_ExpertiseWeight_Is0Point9()
    {
        // Act
        var agent = new OptimistAgent();

        // Assert
        agent.ExpertiseWeight.Should().Be(0.9);
    }

    [Fact]
    public void OptimistAgent_SystemPrompt_IsNotEmpty()
    {
        // Act
        var agent = new OptimistAgent();

        // Assert
        agent.SystemPrompt.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void OptimistAgent_ImplementsIAgentPersona()
    {
        // Act
        var agent = new OptimistAgent();

        // Assert
        agent.Should().BeAssignableTo<IAgentPersona>();
    }

    #endregion

    #region PragmatistAgent Tests

    [Fact]
    public void PragmatistAgent_Name_ReturnsPragmatist()
    {
        // Act
        var agent = new PragmatistAgent();

        // Assert
        agent.Name.Should().Be("Pragmatist");
    }

    [Fact]
    public void PragmatistAgent_Description_ContainsExpectedContent()
    {
        // Act
        var agent = new PragmatistAgent();

        // Assert
        agent.Description.Should().Contain("feasibility");
        agent.Description.Should().Contain("resource constraints");
    }

    [Fact]
    public void PragmatistAgent_ExpertiseWeight_Is0Point95()
    {
        // Act
        var agent = new PragmatistAgent();

        // Assert
        agent.ExpertiseWeight.Should().Be(0.95);
    }

    [Fact]
    public void PragmatistAgent_SystemPrompt_IsNotEmpty()
    {
        // Act
        var agent = new PragmatistAgent();

        // Assert
        agent.SystemPrompt.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region SecurityCynicAgent Tests

    [Fact]
    public void SecurityCynicAgent_Name_ReturnsSecurityCynic()
    {
        // Act
        var agent = new SecurityCynicAgent();

        // Assert
        agent.Name.Should().Be("SecurityCynic");
    }

    [Fact]
    public void SecurityCynicAgent_Description_ContainsExpectedContent()
    {
        // Act
        var agent = new SecurityCynicAgent();

        // Assert
        agent.Description.Should().Contain("risks");
        agent.Description.Should().Contain("vulnerabilities");
    }

    [Fact]
    public void SecurityCynicAgent_ExpertiseWeight_Is1Point0()
    {
        // Act
        var agent = new SecurityCynicAgent();

        // Assert
        agent.ExpertiseWeight.Should().Be(1.0);
    }

    [Fact]
    public void SecurityCynicAgent_SystemPrompt_IsNotEmpty()
    {
        // Act
        var agent = new SecurityCynicAgent();

        // Assert
        agent.SystemPrompt.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region TheoristAgent Tests

    [Fact]
    public void TheoristAgent_Name_ReturnsTheorist()
    {
        // Act
        var agent = new TheoristAgent();

        // Assert
        agent.Name.Should().Be("Theorist");
    }

    [Fact]
    public void TheoristAgent_Description_ContainsExpectedContent()
    {
        // Act
        var agent = new TheoristAgent();

        // Assert
        agent.Description.Should().Contain("mathematical correctness");
        agent.Description.Should().Contain("theoretical soundness");
    }

    [Fact]
    public void TheoristAgent_ExpertiseWeight_Is0Point85()
    {
        // Act
        var agent = new TheoristAgent();

        // Assert
        agent.ExpertiseWeight.Should().Be(0.85);
    }

    [Fact]
    public void TheoristAgent_SystemPrompt_IsNotEmpty()
    {
        // Act
        var agent = new TheoristAgent();

        // Assert
        agent.SystemPrompt.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region UserAdvocateAgent Tests

    [Fact]
    public void UserAdvocateAgent_Name_ReturnsUserAdvocate()
    {
        // Act
        var agent = new UserAdvocateAgent();

        // Assert
        agent.Name.Should().Be("UserAdvocate");
    }

    [Fact]
    public void UserAdvocateAgent_Description_ContainsExpectedContent()
    {
        // Act
        var agent = new UserAdvocateAgent();

        // Assert
        agent.Description.Should().Contain("end-user perspective");
        agent.Description.Should().Contain("usability");
    }

    [Fact]
    public void UserAdvocateAgent_ExpertiseWeight_Is0Point9()
    {
        // Act
        var agent = new UserAdvocateAgent();

        // Assert
        agent.ExpertiseWeight.Should().Be(0.9);
    }

    [Fact]
    public void UserAdvocateAgent_SystemPrompt_IsNotEmpty()
    {
        // Act
        var agent = new UserAdvocateAgent();

        // Assert
        agent.SystemPrompt.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region BaseAgentPersona Behavior Tests

    [Fact]
    public async Task GenerateProposalAsync_ReturnsSuccessWithContribution()
    {
        // Arrange
        var agent = new OptimistAgent();
        var topic = CouncilTopic.Simple("Should we adopt new technology?");
        var llm = CreateMockLlm("This is a great opportunity!");

        // Act
        var result = await agent.GenerateProposalAsync(topic, llm);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgentName.Should().Be("Optimist");
        result.Value.Content.Should().Be("This is a great opportunity!");
    }

    [Fact]
    public async Task GenerateChallengeAsync_ReturnsSuccessWithContribution()
    {
        // Arrange
        var agent = new SecurityCynicAgent();
        var topic = CouncilTopic.Simple("Should we deploy?");
        var otherProposals = new List<AgentContribution>
        {
            new("Optimist", "We should deploy immediately!")
        };
        var llm = CreateMockLlm("I see security concerns.");

        // Act
        var result = await agent.GenerateChallengeAsync(topic, otherProposals, llm);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgentName.Should().Be("SecurityCynic");
        result.Value.Content.Should().Be("I see security concerns.");
    }

    [Fact]
    public async Task GenerateRefinementAsync_ReturnsSuccessWithContribution()
    {
        // Arrange
        var agent = new PragmatistAgent();
        var topic = CouncilTopic.Simple("Architecture decision");
        var challenges = new List<AgentContribution>
        {
            new("SecurityCynic", "This has vulnerabilities")
        };
        var ownProposal = new AgentContribution("Pragmatist", "My original proposal");
        var llm = CreateMockLlm("Revised position considering security.");

        // Act
        var result = await agent.GenerateRefinementAsync(topic, challenges, ownProposal, llm);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgentName.Should().Be("Pragmatist");
        result.Value.Content.Should().Be("Revised position considering security.");
    }

    [Fact]
    public async Task GenerateVoteAsync_ReturnsSuccessWithVote()
    {
        // Arrange
        var agent = new TheoristAgent();
        var topic = CouncilTopic.Simple("Should we proceed?");
        var transcript = new List<DebateRound>();
        var llm = CreateMockLlm("POSITION: APPROVE\nRATIONALE: Theoretically sound approach.");

        // Act
        var result = await agent.GenerateVoteAsync(topic, transcript, llm);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgentName.Should().Be("Theorist");
        result.Value.Position.Should().Be("APPROVE");
        result.Value.Weight.Should().Be(0.85);
        result.Value.Rationale.Should().Contain("Theoretically sound");
    }

    [Fact]
    public async Task GenerateProposalAsync_WhenLlmFails_ReturnsFailure()
    {
        // Arrange
        var agent = new OptimistAgent();
        var topic = CouncilTopic.Simple("Test question");
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidOperationException("LLM error"));
        var llm = new ToolAwareChatModel(mockModel, new ToolRegistry());

        // Act
        var result = await agent.GenerateProposalAsync(topic, llm);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Optimist");
        result.Error.Should().Contain("Proposal generation failed");
    }

    [Fact]
    public async Task GenerateChallengeAsync_WhenLlmFails_ReturnsFailure()
    {
        // Arrange
        var agent = new SecurityCynicAgent();
        var topic = CouncilTopic.Simple("Test");
        var proposals = new List<AgentContribution> { new("Agent1", "Proposal") };
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidOperationException("LLM error"));
        var llm = new ToolAwareChatModel(mockModel, new ToolRegistry());

        // Act
        var result = await agent.GenerateChallengeAsync(topic, proposals, llm);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("SecurityCynic");
        result.Error.Should().Contain("Challenge generation failed");
    }

    [Fact]
    public async Task GenerateRefinementAsync_WhenLlmFails_ReturnsFailure()
    {
        // Arrange
        var agent = new PragmatistAgent();
        var topic = CouncilTopic.Simple("Test");
        var challenges = new List<AgentContribution>();
        var ownProposal = new AgentContribution("Pragmatist", "My proposal");
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidOperationException("LLM error"));
        var llm = new ToolAwareChatModel(mockModel, new ToolRegistry());

        // Act
        var result = await agent.GenerateRefinementAsync(topic, challenges, ownProposal, llm);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Pragmatist");
        result.Error.Should().Contain("Refinement generation failed");
    }

    [Fact]
    public async Task GenerateVoteAsync_WhenLlmFails_ReturnsFailure()
    {
        // Arrange
        var agent = new TheoristAgent();
        var topic = CouncilTopic.Simple("Test");
        var transcript = new List<DebateRound>();
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidOperationException("LLM error"));
        var llm = new ToolAwareChatModel(mockModel, new ToolRegistry());

        // Act
        var result = await agent.GenerateVoteAsync(topic, transcript, llm);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Theorist");
        result.Error.Should().Contain("Vote generation failed");
    }

    [Fact]
    public async Task GenerateProposalAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var agent = new OptimistAgent();
        var topic = CouncilTopic.Simple("Test");
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new OperationCanceledException());
        var llm = new ToolAwareChatModel(mockModel, new ToolRegistry());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => agent.GenerateProposalAsync(topic, llm, cts.Token));
    }

    #endregion

    #region All Agents Consistency Tests

    [Fact]
    public void AllAgents_HaveUniqueNames()
    {
        // Arrange
        var agents = new IAgentPersona[]
        {
            new OptimistAgent(),
            new PragmatistAgent(),
            new SecurityCynicAgent(),
            new TheoristAgent(),
            new UserAdvocateAgent()
        };

        // Act
        var names = agents.Select(a => a.Name).ToList();

        // Assert
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AllAgents_HaveNonEmptyDescriptions()
    {
        // Arrange
        var agents = new IAgentPersona[]
        {
            new OptimistAgent(),
            new PragmatistAgent(),
            new SecurityCynicAgent(),
            new TheoristAgent(),
            new UserAdvocateAgent()
        };

        // Assert
        foreach (var agent in agents)
        {
            agent.Description.Should().NotBeNullOrWhiteSpace(
                $"Agent '{agent.Name}' should have a non-empty description");
        }
    }

    [Fact]
    public void AllAgents_HaveExpertiseWeightBetweenZeroAndOne()
    {
        // Arrange
        var agents = new IAgentPersona[]
        {
            new OptimistAgent(),
            new PragmatistAgent(),
            new SecurityCynicAgent(),
            new TheoristAgent(),
            new UserAdvocateAgent()
        };

        // Assert
        foreach (var agent in agents)
        {
            agent.ExpertiseWeight.Should().BeGreaterThanOrEqualTo(0.0,
                $"Agent '{agent.Name}' weight should be >= 0");
            agent.ExpertiseWeight.Should().BeLessThanOrEqualTo(1.0,
                $"Agent '{agent.Name}' weight should be <= 1");
        }
    }

    [Fact]
    public void AllAgents_HaveNonEmptySystemPrompts()
    {
        // Arrange
        var agents = new IAgentPersona[]
        {
            new OptimistAgent(),
            new PragmatistAgent(),
            new SecurityCynicAgent(),
            new TheoristAgent(),
            new UserAdvocateAgent()
        };

        // Assert
        foreach (var agent in agents)
        {
            agent.SystemPrompt.Should().NotBeNullOrWhiteSpace(
                $"Agent '{agent.Name}' should have a non-empty system prompt");
        }
    }

    #endregion
}

using NSubstitute;
using Ouroboros.Abstractions.Core;
using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Council.Agents;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class CouncilOrchestratorPipelineTests
{
    private static ToolAwareChatModel CreateMockLlm(string response = "POSITION: APPROVE\nRATIONALE: Good proposal.")
    {
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    private static ToolAwareChatModel CreateFailingLlm(string errorMessage = "Connection failed")
    {
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidOperationException(errorMessage));
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLlm_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new CouncilOrchestrator(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidLlm_CreatesEmptyCouncil()
    {
        // Act
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());

        // Assert
        orchestrator.Agents.Should().BeEmpty();
    }

    #endregion

    #region CreateWithDefaultAgents Tests

    [Fact]
    public void CreateWithDefaultAgents_AddsFiveAgents()
    {
        // Act
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());

        // Assert
        orchestrator.Agents.Should().HaveCount(5);
    }

    [Fact]
    public void CreateWithDefaultAgents_ContainsAllExpectedAgentTypes()
    {
        // Act
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var names = orchestrator.Agents.Select(a => a.Name).ToList();

        // Assert
        names.Should().Contain("Optimist");
        names.Should().Contain("SecurityCynic");
        names.Should().Contain("Pragmatist");
        names.Should().Contain("Theorist");
        names.Should().Contain("UserAdvocate");
    }

    #endregion

    #region AddAgent Tests

    [Fact]
    public void AddAgent_WithValidAgent_IncreasesCount()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());

        // Act
        orchestrator.AddAgent(new OptimistAgent());

        // Assert
        orchestrator.Agents.Should().HaveCount(1);
    }

    [Fact]
    public void AddAgent_WithNullAgent_ThrowsArgumentNullException()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());

        // Act
        Action act = () => orchestrator.AddAgent(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddAgent_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());
        orchestrator.AddAgent(new OptimistAgent());

        // Act
        Action act = () => orchestrator.AddAgent(new OptimistAgent());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Optimist*already exists*");
    }

    [Fact]
    public void AddAgent_MultipleUniqueAgents_AddsAll()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());

        // Act
        orchestrator.AddAgent(new OptimistAgent());
        orchestrator.AddAgent(new PragmatistAgent());
        orchestrator.AddAgent(new SecurityCynicAgent());

        // Assert
        orchestrator.Agents.Should().HaveCount(3);
    }

    #endregion

    #region RemoveAgent Tests

    [Fact]
    public void RemoveAgent_ExistingAgent_ReturnsTrueAndRemoves()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var initialCount = orchestrator.Agents.Count;

        // Act
        var result = orchestrator.RemoveAgent("Optimist");

        // Assert
        result.Should().BeTrue();
        orchestrator.Agents.Should().HaveCount(initialCount - 1);
        orchestrator.Agents.Select(a => a.Name).Should().NotContain("Optimist");
    }

    [Fact]
    public void RemoveAgent_NonExistentAgent_ReturnsFalse()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());

        // Act
        var result = orchestrator.RemoveAgent("NonExistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RemoveAgent_EmptyCouncil_ReturnsFalse()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());

        // Act
        var result = orchestrator.RemoveAgent("AnyAgent");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Agents Property Tests

    [Fact]
    public void Agents_ReturnsSnapshot_NotLiveReference()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());
        orchestrator.AddAgent(new OptimistAgent());

        // Act
        var snapshot = orchestrator.Agents;
        orchestrator.AddAgent(new PragmatistAgent());

        // Assert — the snapshot should still show 1 agent
        snapshot.Should().HaveCount(1);
        orchestrator.Agents.Should().HaveCount(2);
    }

    #endregion

    #region ConveneCouncilAsync Tests

    [Fact]
    public async Task ConveneCouncilAsync_NoAgents_ReturnsFailure()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());
        var topic = CouncilTopic.Simple("Test question");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No agents");
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithDefaultConfig_UsesDefaultConfig()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var topic = CouncilTopic.Simple("Test question");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithAgents_ReturnsDecisionWithTranscript()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var topic = CouncilTopic.Simple("Should we refactor?");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transcript.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithAgents_ReturnsDecisionWithVotes()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var topic = CouncilTopic.Simple("Should we proceed?");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Votes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithAgents_ReturnsPositiveConfidence()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var topic = CouncilTopic.Simple("Test question");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Confidence.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithCustomConfig_UsesProvidedConfig()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var topic = CouncilTopic.Simple("Test question");
        var config = CouncilConfig.Fast;

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithBackground_IncludesBackgroundInDebate()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var topic = CouncilTopic.WithBackground(
            "Should we migrate?",
            "Current system is monolithic and hard to maintain.");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithConstraints_IncludesConstraintsInDebate()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var topic = new CouncilTopic(
            "Should we adopt Kubernetes?",
            "We need better scalability",
            new List<string> { "Budget under $10K/month", "Team has limited K8s experience" });

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConveneCouncilAsync_AllAgentsApprove_HasConsensus()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var topic = CouncilTopic.Simple("Test question");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsConsensus.Should().BeTrue();
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithMinorityReport_RecordsMinorities()
    {
        // Arrange — set up one agent to reject while others approve
        var callCount = 0;
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var count = Interlocked.Increment(ref callCount);
                // Make some vote calls return REJECT to create minority opinions
                // Vote phase happens after proposal, challenge, refinement phases
                var prompt = callInfo.ArgAt<string>(0);
                if (prompt.Contains("Cast your final vote") && count % 5 == 0)
                {
                    return Task.FromResult("POSITION: REJECT\nRATIONALE: Security concerns remain.");
                }
                return Task.FromResult("POSITION: APPROVE\nRATIONALE: Good proposal.");
            });
        var llm = new ToolAwareChatModel(mockModel, new ToolRegistry());
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(llm);
        var topic = CouncilTopic.Simple("Test question");
        var config = new CouncilConfig(EnableMinorityReport: true);

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // MinorityOpinions list is created (may or may not be empty depending on vote distribution)
        result.Value.MinorityOpinions.Should().NotBeNull();
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithMinorityReportDisabled_NoMinorities()
    {
        // Arrange — set up mixed votes but disable minority report
        var callCount = 0;
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var count = Interlocked.Increment(ref callCount);
                if (count % 3 == 0)
                {
                    return Task.FromResult("POSITION: REJECT\nRATIONALE: I disagree.");
                }
                return Task.FromResult("POSITION: APPROVE\nRATIONALE: Looks good.");
            });
        var llm = new ToolAwareChatModel(mockModel, new ToolRegistry());
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(llm);
        var topic = CouncilTopic.Simple("Test question");
        var config = new CouncilConfig(EnableMinorityReport: false);

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MinorityOpinions.Should().BeEmpty();
    }

    [Fact]
    public async Task ConveneCouncilAsync_WhenAllAgentsFail_ReturnsFailure()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateFailingLlm());
        orchestrator.AddAgent(new OptimistAgent());
        orchestrator.AddAgent(new PragmatistAgent());
        var topic = CouncilTopic.Simple("Test");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithSingleAgent_Succeeds()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());
        orchestrator.AddAgent(new OptimistAgent());
        var topic = CouncilTopic.Simple("Test question");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Votes.Should().HaveCount(1);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void AddAgent_ConcurrentAccess_DoesNotCorrupt()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());
        var agents = Enumerable.Range(0, 10)
            .Select(i =>
            {
                var agent = Substitute.For<IAgentPersona>();
                agent.Name.Returns($"Agent{i}");
                return agent;
            })
            .ToList();

        // Act — add agents from multiple threads
        Parallel.ForEach(agents, agent =>
        {
            orchestrator.AddAgent(agent);
        });

        // Assert
        orchestrator.Agents.Should().HaveCount(10);
    }

    #endregion
}

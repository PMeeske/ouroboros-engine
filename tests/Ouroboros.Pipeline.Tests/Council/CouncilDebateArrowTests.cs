using NSubstitute;
using Ouroboros.Abstractions.Core;
using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Council.Agents;
using LangChain.Databases;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class CouncilDebateArrowTests
{
    private static ToolAwareChatModel CreateMockLlm(string response = "POSITION: APPROVE\nRATIONALE: Good proposal.")
    {
        var mockModel = Substitute.For<IChatCompletionModel>();
        mockModel.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    private static PipelineBranch CreateTestBranch()
    {
        var store = Substitute.For<IVectorStore>();
        var source = DataSource.FromPath("/test");
        return new PipelineBranch("test-branch", store, source);
    }

    private static ICouncilOrchestrator CreateMockOrchestrator(
        Result<CouncilDecision, string>? result = null)
    {
        var orchestrator = Substitute.For<ICouncilOrchestrator>();

        var defaultResult = result ?? Result<CouncilDecision, string>.Success(
            new CouncilDecision(
                "Approved",
                new Dictionary<string, AgentVote>
                {
                    ["Agent1"] = new("Agent1", "APPROVE", 1.0, "Good")
                },
                new List<DebateRound>(),
                0.9,
                new List<MinorityOpinion>()));

        orchestrator.ConveneCouncilAsync(
                Arg.Any<CouncilTopic>(),
                Arg.Any<CouncilConfig>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(defaultResult));

        return orchestrator;
    }

    #region Create Tests

    [Fact]
    public async Task Create_WithSuccessfulOrchestrator_AddDecisionEventToBranch()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var topic = CouncilTopic.Simple("Test question");
        var branch = CreateTestBranch();
        var arrow = CouncilDebateArrow.Create(orchestrator, topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().HaveCount(1);
        result.Events[0].Should().BeOfType<CouncilDecisionEvent>();
    }

    [Fact]
    public async Task Create_WithFailedOrchestrator_AddsFailedDecisionEvent()
    {
        // Arrange
        var failureResult = Result<CouncilDecision, string>.Failure("Debate failed");
        var orchestrator = CreateMockOrchestrator(failureResult);
        var topic = CouncilTopic.Simple("Test question");
        var branch = CreateTestBranch();
        var arrow = CouncilDebateArrow.Create(orchestrator, topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().HaveCount(1);
        var evt = result.Events[0].Should().BeOfType<CouncilDecisionEvent>().Subject;
        evt.Decision.Conclusion.Should().Contain("failed");
    }

    [Fact]
    public async Task Create_WithNullConfig_UsesDefault()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilDebateArrow.Create(orchestrator, topic, null);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().HaveCount(1);
        await orchestrator.Received(1).ConveneCouncilAsync(
            topic,
            Arg.Is<CouncilConfig>(c => c == CouncilConfig.Default),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithCustomConfig_PassesConfigToOrchestrator()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var topic = CouncilTopic.Simple("Test");
        var config = CouncilConfig.Strict;
        var branch = CreateTestBranch();
        var arrow = CouncilDebateArrow.Create(orchestrator, topic, config);

        // Act
        await arrow(branch);

        // Assert
        await orchestrator.Received(1).ConveneCouncilAsync(
            topic,
            config,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_PreservesBranchProperties()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilDebateArrow.Create(orchestrator, topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Name.Should().Be("test-branch");
    }

    #endregion

    #region CreateSafe Tests

    [Fact]
    public async Task CreateSafe_WithSuccessfulOrchestrator_ReturnsSuccess()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilDebateArrow.CreateSafe(orchestrator, topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateSafe_WithFailedOrchestrator_ReturnsFailure()
    {
        // Arrange
        var failureResult = Result<CouncilDecision, string>.Failure("Council error");
        var orchestrator = CreateMockOrchestrator(failureResult);
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilDebateArrow.CreateSafe(orchestrator, topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Council error");
    }

    [Fact]
    public async Task CreateSafe_WithException_ReturnsFailure()
    {
        // Arrange
        var orchestrator = Substitute.For<ICouncilOrchestrator>();
        orchestrator.ConveneCouncilAsync(
                Arg.Any<CouncilTopic>(),
                Arg.Any<CouncilConfig>(),
                Arg.Any<CancellationToken>())
            .Returns<Result<CouncilDecision, string>>(x =>
                throw new InvalidOperationException("Unexpected error"));
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilDebateArrow.CreateSafe(orchestrator, topic);

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Council debate exception");
        result.Error.Should().Contain("Unexpected error");
    }

    [Fact]
    public async Task CreateSafe_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var orchestrator = Substitute.For<ICouncilOrchestrator>();
        orchestrator.ConveneCouncilAsync(
                Arg.Any<CouncilTopic>(),
                Arg.Any<CouncilConfig>(),
                Arg.Any<CancellationToken>())
            .Returns<Result<CouncilDecision, string>>(x =>
                throw new OperationCanceledException());
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var arrow = CouncilDebateArrow.CreateSafe(orchestrator, topic);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => arrow(branch));
    }

    #endregion

    #region CreateDynamic Tests

    [Fact]
    public async Task CreateDynamic_BuildsTopicFromBranch()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var branch = CreateTestBranch();
        CouncilTopic? capturedTopic = null;
        var arrow = CouncilDebateArrow.CreateDynamic(
            orchestrator,
            b =>
            {
                capturedTopic = CouncilTopic.Simple($"Question about {b.Name}");
                return capturedTopic;
            });

        // Act
        var result = await arrow(branch);

        // Assert
        capturedTopic.Should().NotBeNull();
        capturedTopic!.Question.Should().Contain("test-branch");
        result.Events.Should().HaveCount(1);
    }

    #endregion

    #region WithCouncilValidation Tests

    [Fact]
    public async Task WithCouncilValidation_RunsReasoningStepFirst()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var executionOrder = new List<string>();
        Step<PipelineBranch, PipelineBranch> reasoningStep = async branch =>
        {
            executionOrder.Add("reasoning");
            return branch;
        };
        var branch = CreateTestBranch();

        orchestrator.ConveneCouncilAsync(
                Arg.Any<CouncilTopic>(),
                Arg.Any<CouncilConfig>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                executionOrder.Add("council");
                return Task.FromResult(Result<CouncilDecision, string>.Success(
                    new CouncilDecision("OK",
                        new Dictionary<string, AgentVote>(),
                        new List<DebateRound>(), 0.9,
                        new List<MinorityOpinion>())));
            });

        var arrow = CouncilDebateArrow.WithCouncilValidation(
            orchestrator,
            reasoningStep,
            b => CouncilTopic.Simple("Validate"));

        // Act
        await arrow(branch);

        // Assert
        executionOrder.Should().ContainInOrder("reasoning", "council");
    }

    [Fact]
    public async Task WithCouncilValidation_AddsDecisionEventAfterReasoning()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        Step<PipelineBranch, PipelineBranch> reasoningStep = branch =>
            Task.FromResult(branch);
        var branch = CreateTestBranch();

        var arrow = CouncilDebateArrow.WithCouncilValidation(
            orchestrator,
            reasoningStep,
            b => CouncilTopic.Simple("Validate"));

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().HaveCount(1);
        result.Events[0].Should().BeOfType<CouncilDecisionEvent>();
    }

    #endregion

    #region GetMostRecentDecision Tests

    [Fact]
    public void GetMostRecentDecision_WithNoEvents_ReturnsNull()
    {
        // Arrange
        var branch = CreateTestBranch();

        // Act
        var decision = CouncilDebateArrow.GetMostRecentDecision(branch);

        // Assert
        decision.Should().BeNull();
    }

    [Fact]
    public void GetMostRecentDecision_WithDecisionEvent_ReturnsDecision()
    {
        // Arrange
        var branch = CreateTestBranch();
        var topic = CouncilTopic.Simple("Test");
        var councilDecision = new CouncilDecision(
            "Approved",
            new Dictionary<string, AgentVote>(),
            new List<DebateRound>(),
            0.9,
            new List<MinorityOpinion>());
        var evt = CouncilDecisionEvent.Create(topic, councilDecision);
        var branchWithEvent = branch.WithEvent(evt);

        // Act
        var decision = CouncilDebateArrow.GetMostRecentDecision(branchWithEvent);

        // Assert
        decision.Should().NotBeNull();
        decision!.Conclusion.Should().Be("Approved");
    }

    [Fact]
    public void GetMostRecentDecision_WithMultipleEvents_ReturnsNewest()
    {
        // Arrange
        var branch = CreateTestBranch();
        var topic = CouncilTopic.Simple("Test");

        var olderDecision = new CouncilDecision("Old", new Dictionary<string, AgentVote>(),
            new List<DebateRound>(), 0.5, new List<MinorityOpinion>());
        var olderEvt = new CouncilDecisionEvent(
            Guid.NewGuid(), topic, olderDecision, DateTime.UtcNow.AddMinutes(-5));

        var newerDecision = new CouncilDecision("New", new Dictionary<string, AgentVote>(),
            new List<DebateRound>(), 0.9, new List<MinorityOpinion>());
        var newerEvt = new CouncilDecisionEvent(
            Guid.NewGuid(), topic, newerDecision, DateTime.UtcNow);

        var branchWithEvents = branch.WithEvent(olderEvt).WithEvent(newerEvt);

        // Act
        var decision = CouncilDebateArrow.GetMostRecentDecision(branchWithEvents);

        // Assert
        decision.Should().NotBeNull();
        decision!.Conclusion.Should().Be("New");
    }

    #endregion

    #region HasConsensus Tests

    [Fact]
    public void HasConsensus_WithNoEvents_ReturnsFalse()
    {
        // Arrange
        var branch = CreateTestBranch();

        // Act & Assert
        CouncilDebateArrow.HasConsensus(branch).Should().BeFalse();
    }

    [Fact]
    public void HasConsensus_WithConsensusDecision_ReturnsTrue()
    {
        // Arrange
        var branch = CreateTestBranch();
        var topic = CouncilTopic.Simple("Test");
        var votes = new Dictionary<string, AgentVote>
        {
            ["A1"] = new("A1", "APPROVE", 1.0, "R1"),
            ["A2"] = new("A2", "APPROVE", 0.9, "R2")
        };
        var decision = new CouncilDecision("Approved", votes,
            new List<DebateRound>(), 0.95, new List<MinorityOpinion>());
        var evt = CouncilDecisionEvent.Create(topic, decision);
        var branchWithEvent = branch.WithEvent(evt);

        // Act & Assert
        CouncilDebateArrow.HasConsensus(branchWithEvent).Should().BeTrue();
    }

    [Fact]
    public void HasConsensus_WithSplitDecision_ReturnsFalse()
    {
        // Arrange
        var branch = CreateTestBranch();
        var topic = CouncilTopic.Simple("Test");
        var votes = new Dictionary<string, AgentVote>
        {
            ["A1"] = new("A1", "APPROVE", 1.0, "R1"),
            ["A2"] = new("A2", "REJECT", 0.9, "R2")
        };
        var decision = new CouncilDecision("Split", votes,
            new List<DebateRound>(), 0.5, new List<MinorityOpinion>());
        var evt = CouncilDecisionEvent.Create(topic, decision);
        var branchWithEvent = branch.WithEvent(evt);

        // Act & Assert
        CouncilDebateArrow.HasConsensus(branchWithEvent).Should().BeFalse();
    }

    #endregion
}

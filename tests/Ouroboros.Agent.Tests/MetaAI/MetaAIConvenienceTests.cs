using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class MetaAIConvenienceTests
{
    private readonly Mock<Ouroboros.Abstractions.Core.IChatCompletionModel> _mockLlm = new();
    private readonly Mock<IEmbeddingModel> _mockEmbedding = new();

    // === CreateSimple Tests ===

    [Fact]
    public void CreateSimple_ValidLlm_ReturnsSuccess()
    {
        var result = MetaAIConvenience.CreateSimple(_mockLlm.Object);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void CreateSimple_NullLlm_ReturnsFailure()
    {
        var result = MetaAIConvenience.CreateSimple(null!);

        result.IsFailure.Should().BeTrue();
    }

    // === CreateStandard Tests ===

    [Fact]
    public void CreateStandard_ValidArgs_ReturnsSuccess()
    {
        var result = MetaAIConvenience.CreateStandard(_mockLlm.Object, new ToolRegistry(), _mockEmbedding.Object);

        result.IsSuccess.Should().BeTrue();
    }

    // === CreateAdvanced Tests ===

    [Fact]
    public void CreateAdvanced_ValidArgs_ReturnsSuccess()
    {
        var result = MetaAIConvenience.CreateAdvanced(_mockLlm.Object, new ToolRegistry(), _mockEmbedding.Object, 0.8);

        result.IsSuccess.Should().BeTrue();
    }

    // === CreateResearchAssistant Tests ===

    [Fact]
    public void CreateResearchAssistant_ValidArgs_ReturnsSuccess()
    {
        var result = MetaAIConvenience.CreateResearchAssistant(_mockLlm.Object, new ToolRegistry(), _mockEmbedding.Object);

        result.IsSuccess.Should().BeTrue();
    }

    // === CreateCodeAssistant Tests ===

    [Fact]
    public void CreateCodeAssistant_ValidArgs_ReturnsSuccess()
    {
        var result = MetaAIConvenience.CreateCodeAssistant(_mockLlm.Object, new ToolRegistry());

        result.IsSuccess.Should().BeTrue();
    }

    // === CreateChatAssistant Tests ===

    [Fact]
    public void CreateChatAssistant_ValidArgs_ReturnsSuccess()
    {
        var result = MetaAIConvenience.CreateChatAssistant(_mockLlm.Object);

        result.IsSuccess.Should().BeTrue();
    }

    // === AskQuestion Extension Method Tests ===

    [Fact]
    public async Task AskQuestion_PlanFails_ReturnsFailure()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Failure("plan failed"));

        var result = await mockOrchestrator.Object.AskQuestion("test question");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AskQuestion_ExecuteFails_ReturnsFailure()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = CreateSimplePlan("test");

        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));

        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Failure("execution failed"));

        var result = await mockOrchestrator.Object.AskQuestion("test question");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AskQuestion_Success_ReturnsFinalOutput()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = CreateSimplePlan("test");
        var execution = CreateExecution(plan, "the answer");

        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));

        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execution));

        var result = await mockOrchestrator.Object.AskQuestion("test question");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("the answer");
    }

    // === CompleteWorkflow Extension Method Tests ===

    [Fact]
    public async Task CompleteWorkflow_AllStagesSucceed_ReturnsVerification()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = CreateSimplePlan("test");
        var execution = CreateExecution(plan, "output");
        var verification = new PlanVerificationResult(execution, true, 0.9, new List<string>(), new List<string>(), DateTime.UtcNow);

        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execution));
        mockOrchestrator.Setup(o => o.VerifyAsync(It.IsAny<PlanExecutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanVerificationResult, string>.Success(verification));

        var result = await mockOrchestrator.Object.CompleteWorkflow("goal");

        result.IsSuccess.Should().BeTrue();
        result.Value.Verified.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteWorkflow_AutoLearnEnabled_CallsLearn()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = CreateSimplePlan("test");
        var execution = CreateExecution(plan, "output");
        var verification = new PlanVerificationResult(execution, true, 0.9, new List<string>(), new List<string>(), DateTime.UtcNow);

        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execution));
        mockOrchestrator.Setup(o => o.VerifyAsync(It.IsAny<PlanExecutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanVerificationResult, string>.Success(verification));

        await mockOrchestrator.Object.CompleteWorkflow("goal", autoLearn: true);

        mockOrchestrator.Verify(o => o.LearnFromExecution(It.IsAny<PlanVerificationResult>()), Times.Once);
    }

    // === ProcessBatch Extension Method Tests ===

    [Fact]
    public async Task ProcessBatch_MultipleTasks_ProcessesAll()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = CreateSimplePlan("test");

        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(
                CreateExecution(plan, "answer")));

        var results = await mockOrchestrator.Object.ProcessBatch(new[] { "task1", "task2", "task3" });

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
    }

    // === Helper Methods ===

    private static Plan CreateSimplePlan(string goal)
    {
        return new Plan(goal, new List<PlanStep>
        {
            new PlanStep("action", new Dictionary<string, object>(), "expected", 0.8)
        }, new Dictionary<string, double> { ["overall"] = 0.8 }, DateTime.UtcNow);
    }

    private static PlanExecutionResult CreateExecution(Plan plan, string finalOutput)
    {
        var stepResults = new List<StepResult>
        {
            new StepResult(plan.Steps[0], true, finalOutput, null, TimeSpan.FromMilliseconds(50), new Dictionary<string, object>())
        };
        return new PlanExecutionResult(plan, stepResults, true, finalOutput, new Dictionary<string, object>(), TimeSpan.FromSeconds(1));
    }
}

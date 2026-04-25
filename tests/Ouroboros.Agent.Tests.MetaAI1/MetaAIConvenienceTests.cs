using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class MetaAIConvenienceTests
{
    #region CreateSimple

    [Fact]
    public void CreateSimple_WithValidLLM_ShouldReturnSuccess()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var result = MetaAIConvenience.CreateSimple(mockLlm.Object);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void CreateSimple_WithNullLLM_ShouldReturnFailure()
    {
        var result = MetaAIConvenience.CreateSimple(null!);

        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region CreateStandard

    [Fact]
    public void CreateStandard_WithValidComponents_ShouldReturnSuccess()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var tools = ToolRegistry.CreateDefault();
        var mockEmbedding = new Mock<IEmbeddingModel>();

        var result = MetaAIConvenience.CreateStandard(mockLlm.Object, tools, mockEmbedding.Object);

        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region CreateAdvanced

    [Fact]
    public void CreateAdvanced_WithValidComponents_ShouldReturnSuccess()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var tools = ToolRegistry.CreateDefault();
        var mockEmbedding = new Mock<IEmbeddingModel>();

        var result = MetaAIConvenience.CreateAdvanced(mockLlm.Object, tools, mockEmbedding.Object);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CreateAdvanced_WithCustomThreshold_ShouldReturnSuccess()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var tools = ToolRegistry.CreateDefault();
        var mockEmbedding = new Mock<IEmbeddingModel>();

        var result = MetaAIConvenience.CreateAdvanced(mockLlm.Object, tools, mockEmbedding.Object, 0.9);

        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region CreateResearchAssistant

    [Fact]
    public void CreateResearchAssistant_WithValidComponents_ShouldReturnSuccess()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var tools = ToolRegistry.CreateDefault();
        var mockEmbedding = new Mock<IEmbeddingModel>();

        var result = MetaAIConvenience.CreateResearchAssistant(mockLlm.Object, tools, mockEmbedding.Object);

        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region CreateCodeAssistant

    [Fact]
    public void CreateCodeAssistant_WithValidComponents_ShouldReturnSuccess()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var tools = ToolRegistry.CreateDefault();

        var result = MetaAIConvenience.CreateCodeAssistant(mockLlm.Object, tools);

        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region CreateChatAssistant

    [Fact]
    public void CreateChatAssistant_WithValidLLM_ShouldReturnSuccess()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();

        var result = MetaAIConvenience.CreateChatAssistant(mockLlm.Object);

        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region AskQuestion

    [Fact]
    public async Task AskQuestion_PlanFails_ShouldReturnFailure()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Failure("Planning failed"));

        var result = await mockOrchestrator.Object.AskQuestion("question");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Planning failed");
    }

    [Fact]
    public async Task AskQuestion_ExecuteFails_ShouldReturnFailure()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = new Plan("goal", new List<PlanStep>());
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Failure("Execution failed"));

        var result = await mockOrchestrator.Object.AskQuestion("question");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Execution failed");
    }

    [Fact]
    public async Task AskQuestion_Success_ShouldReturnOutput()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = new Plan("goal", new List<PlanStep>());
        var execResult = new PlanExecutionResult(new List<StepResult>(), "final output", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>());
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execResult));

        var result = await mockOrchestrator.Object.AskQuestion("question");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("final output");
    }

    [Fact]
    public async Task AskQuestion_NullOutput_ShouldReturnDefaultMessage()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = new Plan("goal", new List<PlanStep>());
        var execResult = new PlanExecutionResult(new List<StepResult>(), null!, TimeSpan.FromSeconds(1), true, new Dictionary<string, object>());
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execResult));

        var result = await mockOrchestrator.Object.AskQuestion("question");

        result.Value.Should().Be("No output generated");
    }

    #endregion

    #region AnalyzeText

    [Fact]
    public async Task AnalyzeText_Success_ShouldReturnAnalysisAndQuality()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = new Plan("goal", new List<PlanStep>());
        var execResult = new PlanExecutionResult(new List<StepResult>(), "analysis result", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>());
        var verifyResult = new PlanVerificationResult(true, 0.9, new List<string>(), new Dictionary<string, double>());
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execResult));
        mockOrchestrator.Setup(o => o.VerifyAsync(It.IsAny<PlanExecutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanVerificationResult, string>.Success(verifyResult));

        var result = await mockOrchestrator.Object.AnalyzeText("text to analyze");

        result.IsSuccess.Should().BeTrue();
        result.Value.analysis.Should().Be("analysis result");
        result.Value.quality.Should().Be(0.9);
    }

    #endregion

    #region GenerateCode

    [Fact]
    public async Task GenerateCode_Success_ShouldReturnCode()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = new Plan("goal", new List<PlanStep>());
        var execResult = new PlanExecutionResult(new List<StepResult>(), "code output", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>());
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execResult));

        var result = await mockOrchestrator.Object.GenerateCode("Create a function");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("code output");
    }

    [Fact]
    public async Task GenerateCode_WithLanguage_ShouldPassLanguage()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = new Plan("goal", new List<PlanStep>());
        var execResult = new PlanExecutionResult(new List<StepResult>(), "python code", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>());
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execResult));

        var result = await mockOrchestrator.Object.GenerateCode("Create a function", "Python");

        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region CompleteWorkflow

    [Fact]
    public async Task CompleteWorkflow_WithAutoLearn_Verified_ShouldLearn()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = new Plan("goal", new List<PlanStep>());
        var execResult = new PlanExecutionResult(new List<StepResult>(), "output", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>());
        var verifyResult = new PlanVerificationResult(true, 0.95, new List<string>(), new Dictionary<string, double>());
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execResult));
        mockOrchestrator.Setup(o => o.VerifyAsync(It.IsAny<PlanExecutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanVerificationResult, string>.Success(verifyResult));

        var result = await mockOrchestrator.Object.CompleteWorkflow("goal", autoLearn: true);

        result.IsSuccess.Should().BeTrue();
        mockOrchestrator.Verify(o => o.LearnFromExecution(verifyResult), Times.Once);
    }

    [Fact]
    public async Task CompleteWorkflow_WithAutoLearn_NotVerified_ShouldNotLearn()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = new Plan("goal", new List<PlanStep>());
        var execResult = new PlanExecutionResult(new List<StepResult>(), "output", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>());
        var verifyResult = new PlanVerificationResult(false, 0.3, new List<string>(), new Dictionary<string, double>());
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execResult));
        mockOrchestrator.Setup(o => o.VerifyAsync(It.IsAny<PlanExecutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanVerificationResult, string>.Success(verifyResult));

        var result = await mockOrchestrator.Object.CompleteWorkflow("goal", autoLearn: true);

        result.IsSuccess.Should().BeTrue();
        mockOrchestrator.Verify(o => o.LearnFromExecution(It.IsAny<PlanVerificationResult>()), Times.Never);
    }

    [Fact]
    public async Task CompleteWorkflow_WithoutAutoLearn_ShouldNotLearn()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = new Plan("goal", new List<PlanStep>());
        var execResult = new PlanExecutionResult(new List<StepResult>(), "output", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>());
        var verifyResult = new PlanVerificationResult(true, 0.95, new List<string>(), new Dictionary<string, double>());
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execResult));
        mockOrchestrator.Setup(o => o.VerifyAsync(It.IsAny<PlanExecutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanVerificationResult, string>.Success(verifyResult));

        var result = await mockOrchestrator.Object.CompleteWorkflow("goal", autoLearn: false);

        result.IsSuccess.Should().BeTrue();
        mockOrchestrator.Verify(o => o.LearnFromExecution(It.IsAny<PlanVerificationResult>()), Times.Never);
    }

    #endregion

    #region ProcessBatch

    [Fact]
    public async Task ProcessBatch_SingleTask_ShouldReturnSingleResult()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = new Plan("goal", new List<PlanStep>());
        var execResult = new PlanExecutionResult(new List<StepResult>(), "output", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>());
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execResult));

        var results = await mockOrchestrator.Object.ProcessBatch(new List<string> { "task1" });

        results.Should().ContainSingle();
        results[0].IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessBatch_MultipleTasks_ShouldReturnMultipleResults()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var plan = new Plan("goal", new List<PlanStep>());
        var execResult = new PlanExecutionResult(new List<StepResult>(), "output", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>());
        mockOrchestrator.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));
        mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execResult));

        var results = await mockOrchestrator.Object.ProcessBatch(new List<string> { "task1", "task2", "task3" });

        results.Should().HaveCount(3);
    }

    #endregion
}

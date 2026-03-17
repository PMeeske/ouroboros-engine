using FluentAssertions;
using Ouroboros.Agent;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class SmartModelOrchestratorTests
{
    private readonly Mock<Ouroboros.Abstractions.Core.IChatCompletionModel> _mockModel = new();

    private SmartModelOrchestrator CreateSut(string fallback = "default")
    {
        return new SmartModelOrchestrator(ToolRegistry.CreateDefault(), fallback);
    }

    // === Constructor Tests ===

    [Fact]
    public void Constructor_NullTools_ThrowsArgumentNullException()
    {
        var act = () => new SmartModelOrchestrator(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("baseTools");
    }

    [Fact]
    public void Constructor_ValidArgs_DoesNotThrow()
    {
        var act = () => CreateSut();
        act.Should().NotThrow();
    }

    // === RegisterModel Tests ===

    [Fact]
    public void RegisterModel_NullCapability_ThrowsArgumentNullException()
    {
        var sut = CreateSut();
        var act = () => sut.RegisterModel(null!, _mockModel.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("capability");
    }

    [Fact]
    public void RegisterModel_NullModel_ThrowsArgumentNullException()
    {
        var sut = CreateSut();
        var capability = CreateCapability("test-model");
        var act = () => sut.RegisterModel(capability, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("model");
    }

    [Fact]
    public void RegisterModel_ValidArgs_Succeeds()
    {
        var sut = CreateSut();
        var capability = CreateCapability("test-model");

        var act = () => sut.RegisterModel(capability, _mockModel.Object);

        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterModel_CapabilityOnly_Succeeds()
    {
        var sut = CreateSut();
        var capability = CreateCapability("test-model");

        var act = () => sut.RegisterModel(capability);

        act.Should().NotThrow();
    }

    // === SelectModelAsync Tests ===

    [Fact]
    public async Task SelectModelAsync_EmptyPrompt_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.SelectModelAsync("");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task SelectModelAsync_WhitespacePrompt_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.SelectModelAsync("   ");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SelectModelAsync_NoModelsRegistered_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.SelectModelAsync("test prompt");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No models registered");
    }

    [Fact]
    public async Task SelectModelAsync_WithRegisteredModel_ReturnsSuccess()
    {
        var sut = CreateSut();
        var capability = CreateCapability("test-model");
        sut.RegisterModel(capability, _mockModel.Object);

        var result = await sut.SelectModelAsync("test prompt");

        result.IsSuccess.Should().BeTrue();
        result.Value.ModelName.Should().Be("test-model");
    }

    [Fact]
    public async Task SelectModelAsync_CodePrompt_FavorsCodeModel()
    {
        var sut = CreateSut();
        var codeCapability = new ModelCapability("code-model", new[] { "code", "debugging" }, 4096, 1.0, 500, ModelType.Code);
        var generalCapability = new ModelCapability("general-model", new[] { "general" }, 4096, 1.0, 500, ModelType.General);

        sut.RegisterModel(codeCapability, _mockModel.Object);
        sut.RegisterModel(generalCapability, _mockModel.Object);

        var result = await sut.SelectModelAsync("implement a function to sort arrays");

        result.IsSuccess.Should().BeTrue();
        result.Value.ModelName.Should().Be("code-model");
    }

    // === ClassifyUseCase Tests ===

    [Fact]
    public void ClassifyUseCase_CodePrompt_ReturnsCodeGeneration()
    {
        var sut = CreateSut();

        var useCase = sut.ClassifyUseCase("implement a method to sort data");

        useCase.Type.Should().Be(UseCaseType.CodeGeneration);
    }

    [Fact]
    public void ClassifyUseCase_ReasoningPrompt_ReturnsReasoning()
    {
        var sut = CreateSut();

        var useCase = sut.ClassifyUseCase("analyze why the system crashed");

        useCase.Type.Should().Be(UseCaseType.Reasoning);
    }

    [Fact]
    public void ClassifyUseCase_CreativePrompt_ReturnsCreative()
    {
        var sut = CreateSut();

        var useCase = sut.ClassifyUseCase("create a story about a dragon");

        useCase.Type.Should().Be(UseCaseType.Creative);
    }

    [Fact]
    public void ClassifyUseCase_SummarizePrompt_ReturnsSummarization()
    {
        var sut = CreateSut();

        var useCase = sut.ClassifyUseCase("summarize this document");

        useCase.Type.Should().Be(UseCaseType.Summarization);
    }

    [Fact]
    public void ClassifyUseCase_VisionPrompt_ReturnsAnalysis()
    {
        var sut = CreateSut();

        var useCase = sut.ClassifyUseCase("look at this image and describe it");

        useCase.Type.Should().Be(UseCaseType.Analysis);
    }

    [Fact]
    public void ClassifyUseCase_ToolUsePrompt_ReturnsToolUse()
    {
        var sut = CreateSut();

        var useCase = sut.ClassifyUseCase("[TOOL: search] find information");

        useCase.Type.Should().Be(UseCaseType.ToolUse);
    }

    [Fact]
    public void ClassifyUseCase_GenericPrompt_ReturnsConversation()
    {
        var sut = CreateSut();

        var useCase = sut.ClassifyUseCase("hello there");

        useCase.Type.Should().Be(UseCaseType.Conversation);
    }

    // === RecordMetric Tests ===

    [Fact]
    public void RecordMetric_NewResource_CreatesEntry()
    {
        var sut = CreateSut();

        sut.RecordMetric("test-model", 100.0, true);

        var metrics = sut.GetMetrics();
        metrics.Should().ContainKey("test-model");
        metrics["test-model"].ExecutionCount.Should().Be(1);
    }

    [Fact]
    public void RecordMetric_ExistingResource_UpdatesEntry()
    {
        var sut = CreateSut();

        sut.RecordMetric("test-model", 100.0, true);
        sut.RecordMetric("test-model", 200.0, false);

        var metrics = sut.GetMetrics();
        metrics["test-model"].ExecutionCount.Should().Be(2);
        metrics["test-model"].AverageLatencyMs.Should().BeApproximately(150.0, 0.01);
        metrics["test-model"].SuccessRate.Should().BeApproximately(0.5, 0.01);
    }

    // === RecordMetricAsync Tests ===

    [Fact]
    public async Task RecordMetricAsync_NewResource_CreatesEntry()
    {
        var sut = CreateSut();

        await sut.RecordMetricAsync("test-model", 100.0, true);

        var metrics = sut.GetMetrics();
        metrics.Should().ContainKey("test-model");
    }

    // === GetMetrics Tests ===

    [Fact]
    public void GetMetrics_Empty_ReturnsEmptyDictionary()
    {
        var sut = CreateSut();

        var metrics = sut.GetMetrics();

        metrics.Should().BeEmpty();
    }

    // === Dispose Tests ===

    [Fact]
    public void Dispose_MultipleDispose_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        act.Should().NotThrow();
    }

    // === CreateAsync Tests ===

    [Fact]
    public async Task CreateAsync_NullTools_ThrowsArgumentNullException()
    {
        var mockStore = new Mock<IMetricsStore>();
        mockStore.Setup(s => s.GetAllMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, PerformanceMetrics>());

        var act = () => SmartModelOrchestrator.CreateAsync(null!, mockStore.Object);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_NullStore_ThrowsArgumentNullException()
    {
        var act = () => SmartModelOrchestrator.CreateAsync(ToolRegistry.CreateDefault(), null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_ValidArgs_LoadsPersistedMetrics()
    {
        var existingMetrics = new Dictionary<string, PerformanceMetrics>
        {
            ["model-a"] = new PerformanceMetrics("model-a", 10, 200.0, 0.95, DateTime.UtcNow, new Dictionary<string, double>())
        };

        var mockStore = new Mock<IMetricsStore>();
        mockStore.Setup(s => s.GetAllMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMetrics);

        var sut = await SmartModelOrchestrator.CreateAsync(ToolRegistry.CreateDefault(), mockStore.Object);

        var metrics = sut.GetMetrics();
        metrics.Should().ContainKey("model-a");
    }

    // === Helper Methods ===

    private static ModelCapability CreateCapability(string name)
    {
        return new ModelCapability(name, new[] { "general" }, 4096, 1.0, 500.0, ModelType.General);
    }
}

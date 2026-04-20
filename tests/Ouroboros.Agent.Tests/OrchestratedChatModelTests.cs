using FluentAssertions;
using Ouroboros.Agent;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class OrchestratedChatModelTests
{
    private readonly Mock<IModelOrchestrator> _mockOrchestrator = new();

    // === Constructor Tests ===

    [Fact]
    public void Constructor_NullOrchestrator_ThrowsArgumentNullException()
    {
        var act = () => new OrchestratedChatModel(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("orchestrator");
    }

    [Fact]
    public void Constructor_ValidOrchestrator_DoesNotThrow()
    {
        var act = () => new OrchestratedChatModel(_mockOrchestrator.Object);
        act.Should().NotThrow();
    }

    // === GenerateTextAsync Tests ===

    [Fact]
    public async Task GenerateTextAsync_SelectionFails_ReturnsErrorMessage()
    {
        _mockOrchestrator.Setup(o => o.SelectModelAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorDecision, string>.Failure("no model available"));

        var sut = new OrchestratedChatModel(_mockOrchestrator.Object);

        var result = await sut.GenerateTextAsync("test prompt");

        result.Should().Contain("[orchestrator-error]");
        result.Should().Contain("no model available");
    }

    [Fact]
    public async Task GenerateTextAsync_ModelSucceeds_ReturnsModelOutput()
    {
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("model response");

        var decision = new OrchestratorDecision(
            mockModel.Object, "test-model", "reason",
            ToolRegistry.CreateDefault(), 0.9);

        _mockOrchestrator.Setup(o => o.SelectModelAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorDecision, string>.Success(decision));

        var sut = new OrchestratedChatModel(_mockOrchestrator.Object);

        var result = await sut.GenerateTextAsync("test prompt");

        result.Should().Be("model response");
    }

    [Fact]
    public async Task GenerateTextAsync_TrackMetricsEnabled_RecordsMetric()
    {
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("response");

        var decision = new OrchestratorDecision(
            mockModel.Object, "test-model", "reason",
            ToolRegistry.CreateDefault(), 0.9);

        _mockOrchestrator.Setup(o => o.SelectModelAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorDecision, string>.Success(decision));

        var sut = new OrchestratedChatModel(_mockOrchestrator.Object, trackMetrics: true);

        await sut.GenerateTextAsync("test prompt");

        _mockOrchestrator.Verify(o => o.RecordMetric("test-model", It.IsAny<double>(), true), Times.Once);
    }

    [Fact]
    public async Task GenerateTextAsync_TrackMetricsDisabled_DoesNotRecordMetric()
    {
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("response");

        var decision = new OrchestratorDecision(
            mockModel.Object, "test-model", "reason",
            ToolRegistry.CreateDefault(), 0.9);

        _mockOrchestrator.Setup(o => o.SelectModelAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorDecision, string>.Success(decision));

        var sut = new OrchestratedChatModel(_mockOrchestrator.Object, trackMetrics: false);

        await sut.GenerateTextAsync("test prompt");

        _mockOrchestrator.Verify(o => o.RecordMetric(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task GenerateTextAsync_ModelThrows_ReturnsExceptionMessage()
    {
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("model error"));

        var decision = new OrchestratorDecision(
            mockModel.Object, "test-model", "reason",
            ToolRegistry.CreateDefault(), 0.9);

        _mockOrchestrator.Setup(o => o.SelectModelAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorDecision, string>.Success(decision));

        var sut = new OrchestratedChatModel(_mockOrchestrator.Object);

        var result = await sut.GenerateTextAsync("test prompt");

        result.Should().Contain("[orchestrator-exception]");
        result.Should().Contain("model error");
    }
}

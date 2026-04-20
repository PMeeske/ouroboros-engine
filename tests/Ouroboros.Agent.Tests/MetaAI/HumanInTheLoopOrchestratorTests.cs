// <copyright file="HumanInTheLoopOrchestratorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Unit tests for the HumanInTheLoopOrchestrator class.
/// </summary>
[Trait("Category", "Unit")]
public class HumanInTheLoopOrchestratorTests
{
    private readonly Mock<IMetaAIPlannerOrchestrator> _mockOrchestrator;
    private readonly Mock<IHumanFeedbackProvider> _mockFeedback;
    private readonly HumanInTheLoopOrchestrator _sut;

    public HumanInTheLoopOrchestratorTests()
    {
        _mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        _mockFeedback = new Mock<IHumanFeedbackProvider>();
        _sut = new HumanInTheLoopOrchestrator(_mockOrchestrator.Object, _mockFeedback.Object);
    }

    [Fact]
    public void Constructor_NullOrchestrator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new HumanInTheLoopOrchestrator(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullFeedbackProvider_UsesDefaultProvider()
    {
        // Act
        var act = () => new HumanInTheLoopOrchestrator(_mockOrchestrator.Object, null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetFeedbackProvider_NullProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.SetFeedbackProvider(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetFeedbackProvider_ValidProvider_DoesNotThrow()
    {
        // Act
        var act = () => _sut.SetFeedbackProvider(_mockFeedback.Object);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ExecuteWithHumanOversightAsync_NonCriticalSteps_ExecutesWithoutApproval()
    {
        // Arrange
        var step = new PlanStep(
            "analyze",
            new Dictionary<string, object>(),
            "Expected outcome",
            0.9);
        var plan = new Plan(
            "test goal",
            new List<PlanStep> { step },
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        var executionResult = new PlanExecutionResult(
            plan,
            new List<StepResult>
            {
                new StepResult(step, true, "output", null, TimeSpan.FromMilliseconds(100),
                    new Dictionary<string, object>())
            },
            true,
            "output",
            new Dictionary<string, object>(),
            TimeSpan.FromMilliseconds(100));

        _mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(executionResult));

        // Act
        var result = await _sut.ExecuteWithHumanOversightAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockFeedback.Verify(f => f.RequestApprovalAsync(
            It.IsAny<ApprovalRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteWithHumanOversightAsync_CriticalStep_RequestsApproval()
    {
        // Arrange
        var step = new PlanStep(
            "delete data",
            new Dictionary<string, object>(),
            "Delete records",
            0.9);
        var plan = new Plan(
            "cleanup",
            new List<PlanStep> { step },
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        var config = new HumanInTheLoopConfig(
            RequireApprovalForCriticalSteps: true,
            DefaultTimeout: TimeSpan.FromMinutes(5),
            CriticalActionPatterns: new List<string> { "delete", "remove", "drop" });

        var approval = new ApprovalResponse(
            "req-1", true, null, null, DateTime.UtcNow);
        _mockFeedback.Setup(f => f.RequestApprovalAsync(
                It.IsAny<ApprovalRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);

        var executionResult = new PlanExecutionResult(
            plan,
            new List<StepResult>
            {
                new StepResult(step, true, "deleted", null, TimeSpan.FromMilliseconds(100),
                    new Dictionary<string, object>())
            },
            true, "deleted",
            new Dictionary<string, object>(),
            TimeSpan.FromMilliseconds(100));

        _mockOrchestrator.Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(executionResult));

        // Act
        var result = await _sut.ExecuteWithHumanOversightAsync(plan, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockFeedback.Verify(f => f.RequestApprovalAsync(
            It.IsAny<ApprovalRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteWithHumanOversightAsync_CriticalStepRejected_SkipsStep()
    {
        // Arrange
        var step = new PlanStep(
            "delete data",
            new Dictionary<string, object>(),
            "Delete records",
            0.9);
        var plan = new Plan(
            "cleanup",
            new List<PlanStep> { step },
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        var config = new HumanInTheLoopConfig(
            RequireApprovalForCriticalSteps: true,
            DefaultTimeout: TimeSpan.FromMinutes(5),
            CriticalActionPatterns: new List<string> { "delete" });

        var rejection = new ApprovalResponse(
            "req-1", false, "Too risky", null, DateTime.UtcNow);
        _mockFeedback.Setup(f => f.RequestApprovalAsync(
                It.IsAny<ApprovalRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rejection);

        // Act
        var result = await _sut.ExecuteWithHumanOversightAsync(plan, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockOrchestrator.Verify(o => o.ExecuteAsync(
            It.IsAny<Plan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefinePlanInteractivelyAsync_ApproveResponse_ReturnsPlanAsIs()
    {
        // Arrange
        var plan = new Plan(
            "test goal",
            new List<PlanStep>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        var feedback = new HumanFeedbackResponse(
            "req-1", "approve", null, DateTime.UtcNow);

        _mockFeedback.Setup(f => f.RequestFeedbackAsync(
                It.IsAny<HumanFeedbackRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        // Act
        var result = await _sut.RefinePlanInteractivelyAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Goal.Should().Be("test goal");
    }

    [Fact]
    public async Task RefinePlanInteractivelyAsync_ReplanResponse_CallsOrchestrator()
    {
        // Arrange
        var plan = new Plan(
            "test goal",
            new List<PlanStep>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        var feedback = new HumanFeedbackResponse(
            "req-1", "replan", null, DateTime.UtcNow);

        _mockFeedback.Setup(f => f.RequestFeedbackAsync(
                It.IsAny<HumanFeedbackRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        var newPlan = new Plan(
            "test goal",
            new List<PlanStep> { new PlanStep("step1", new(), "outcome", 0.9) },
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        _mockOrchestrator.Setup(o => o.PlanAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(newPlan));

        // Act
        var result = await _sut.RefinePlanInteractivelyAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockOrchestrator.Verify(o => o.PlanAsync(
            "test goal",
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefinePlanInteractivelyAsync_AddStepResponse_AddsStepToPlan()
    {
        // Arrange
        var plan = new Plan(
            "test goal",
            new List<PlanStep>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        var addFeedback = new HumanFeedbackResponse(
            "req-1", "add step", null, DateTime.UtcNow);
        var stepDetailFeedback = new HumanFeedbackResponse(
            "req-2", "validate|input data|validated output", null, DateTime.UtcNow);

        _mockFeedback.SetupSequence(f => f.RequestFeedbackAsync(
                It.IsAny<HumanFeedbackRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(addFeedback)
            .ReturnsAsync(stepDetailFeedback);

        // Act
        var result = await _sut.RefinePlanInteractivelyAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps.Should().HaveCount(1);
        result.Value.Steps[0].Action.Should().Be("validate");
    }
}

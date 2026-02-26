// <copyright file="PlanCommandHandlerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Dispatch;
using MetaAIPlan = Ouroboros.Agent.MetaAI.Plan;
using MetaAIPlanStep = Ouroboros.Agent.MetaAI.PlanStep;

namespace Ouroboros.Tests.Dispatch;

[Trait("Category", "Unit")]
public class PlanCommandHandlerTests
{
    private readonly Mock<IMetaAIPlannerOrchestrator> _plannerMock = new();

    [Fact]
    public async Task CreatePlanCommandHandler_DelegatesToPlanner()
    {
        // Arrange
        var plan = new MetaAIPlan("goal", new List<MetaAIPlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
        _plannerMock
            .Setup(p => p.PlanAsync("goal", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MetaAIPlan, string>.Success(plan));

        var handler = new CreatePlanCommandHandler(_plannerMock.Object);
        var command = new CreatePlanCommand("goal");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Goal.Should().Be("goal");
    }

    [Fact]
    public async Task ExecutePlanCommandHandler_DelegatesToPlanner()
    {
        // Arrange
        var plan = new MetaAIPlan("g", new List<MetaAIPlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
        var execResult = new PlanExecutionResult(
            plan,
            new List<StepResult>(),
            true,
            "done",
            new Dictionary<string, object>(),
            TimeSpan.FromSeconds(1));
        _plannerMock
            .Setup(p => p.ExecuteAsync(plan, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execResult));

        var handler = new ExecutePlanCommandHandler(_plannerMock.Object);
        var command = new ExecutePlanCommand(plan);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
    }
}

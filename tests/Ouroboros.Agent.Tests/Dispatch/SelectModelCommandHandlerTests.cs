// <copyright file="SelectModelCommandHandlerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Dispatch;

namespace Ouroboros.Tests.Dispatch;

[Trait("Category", "Unit")]
public class SelectModelCommandHandlerTests
{
    private readonly Mock<IModelOrchestrator> _orchestratorMock = new();

    [Fact]
    public async Task HandleAsync_DelegatesToOrchestrator()
    {
        // Arrange
        var llmMock = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var decision = new OrchestratorDecision(llmMock.Object, "model-x", "reason", new ToolRegistry(), 0.9);
        var expected = Result<OrchestratorDecision, string>.Success(decision);
        _orchestratorMock
            .Setup(o => o.SelectModelAsync("prompt", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new SelectModelCommandHandler(_orchestratorMock.Object);
        var command = new SelectModelCommand("prompt");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(decision);
    }

    [Fact]
    public async Task HandleAsync_PassesContext()
    {
        // Arrange
        var ctx = new Dictionary<string, object> { ["key"] = "val" };
        var llmMock2 = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var decision = new OrchestratorDecision(llmMock2.Object, "m", "r", new ToolRegistry(), 0.5);
        _orchestratorMock
            .Setup(o => o.SelectModelAsync("p", ctx, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorDecision, string>.Success(decision));

        var handler = new SelectModelCommandHandler(_orchestratorMock.Object);
        var command = new SelectModelCommand("p", ctx);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _orchestratorMock.Verify(o => o.SelectModelAsync("p", ctx, It.IsAny<CancellationToken>()), Times.Once);
    }
}

// <copyright file="QueryHandlerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Dispatch;

namespace Ouroboros.Tests.Dispatch;

[Trait("Category", "Unit")]
public class QueryHandlerTests
{
    private readonly Mock<IModelOrchestrator> _orchestratorMock = new();

    [Fact]
    public async Task ClassifyUseCaseQueryHandler_DelegatesToOrchestrator()
    {
        // Arrange
        var expected = new UseCase(UseCaseType.CodeGeneration, 1, new[] { "code" }, 0.5, 0.5);
        _orchestratorMock.Setup(o => o.ClassifyUseCase("test prompt")).Returns(expected);
        var handler = new ClassifyUseCaseQueryHandler(_orchestratorMock.Object);
        var query = new ClassifyUseCaseQuery("test prompt");

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Should().Be(expected);
        _orchestratorMock.Verify(o => o.ClassifyUseCase("test prompt"), Times.Once);
    }

    [Fact]
    public async Task GetOrchestratorMetricsQueryHandler_ReturnsMetrics()
    {
        // Arrange
        var metrics = new Dictionary<string, PerformanceMetrics>() as IReadOnlyDictionary<string, PerformanceMetrics>;
        _orchestratorMock.Setup(o => o.GetMetrics()).Returns(metrics);
        var handler = new GetOrchestratorMetricsQueryHandler(_orchestratorMock.Object);
        var query = new GetOrchestratorMetricsQuery("Smart");

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Should().BeSameAs(metrics);
    }
}

// <copyright file="CachingModelOrchestratorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Unit tests for the CachingModelOrchestrator decorator.
/// </summary>
[Trait("Category", "Unit")]
public class CachingModelOrchestratorTests
{
    private readonly Mock<IModelOrchestrator> _mockInner;
    private readonly Mock<IOrchestrationCache> _mockCache;
    private readonly CachingModelOrchestrator _sut;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public CachingModelOrchestratorTests()
    {
        _mockInner = new Mock<IModelOrchestrator>();
        _mockCache = new Mock<IOrchestrationCache>();
        _sut = new CachingModelOrchestrator(_mockInner.Object, _mockCache.Object, _ttl);
    }

    [Fact]
    public void Constructor_NullInner_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CachingModelOrchestrator(null!, _mockCache.Object, _ttl);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullCache_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CachingModelOrchestrator(_mockInner.Object, null!, _ttl);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SelectModelAsync_CacheHit_ReturnsFromCache()
    {
        // Arrange
        var decision = new OrchestratorDecision("model-1", "reason", UseCase.Chat, 0.9);
        _mockCache.Setup(c => c.GetCachedDecisionAsync(It.IsAny<string>()))
            .ReturnsAsync(Option<OrchestratorDecision>.Some(decision));

        // Act
        var result = await _sut.SelectModelAsync("test prompt");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(decision);
        _mockInner.Verify(i => i.SelectModelAsync(It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SelectModelAsync_CacheMiss_CallsInnerOrchestrator()
    {
        // Arrange
        var decision = new OrchestratorDecision("model-1", "reason", UseCase.Chat, 0.9);
        _mockCache.Setup(c => c.GetCachedDecisionAsync(It.IsAny<string>()))
            .ReturnsAsync(Option<OrchestratorDecision>.None());
        _mockInner.Setup(i => i.SelectModelAsync(It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorDecision, string>.Success(decision));

        // Act
        var result = await _sut.SelectModelAsync("test prompt");

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockInner.Verify(i => i.SelectModelAsync("test prompt",
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectModelAsync_CacheMissAndSuccess_CachesResult()
    {
        // Arrange
        var decision = new OrchestratorDecision("model-1", "reason", UseCase.Chat, 0.9);
        _mockCache.Setup(c => c.GetCachedDecisionAsync(It.IsAny<string>()))
            .ReturnsAsync(Option<OrchestratorDecision>.None());
        _mockInner.Setup(i => i.SelectModelAsync(It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorDecision, string>.Success(decision));

        // Act
        await _sut.SelectModelAsync("test prompt");

        // Assert
        _mockCache.Verify(c => c.CacheDecisionAsync(
            It.IsAny<string>(), decision, _ttl), Times.Once);
    }

    [Fact]
    public async Task SelectModelAsync_CacheMissAndFailure_DoesNotCache()
    {
        // Arrange
        _mockCache.Setup(c => c.GetCachedDecisionAsync(It.IsAny<string>()))
            .ReturnsAsync(Option<OrchestratorDecision>.None());
        _mockInner.Setup(i => i.SelectModelAsync(It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorDecision, string>.Failure("error"));

        // Act
        var result = await _sut.SelectModelAsync("test prompt");

        // Assert
        result.IsSuccess.Should().BeFalse();
        _mockCache.Verify(c => c.CacheDecisionAsync(
            It.IsAny<string>(),
            It.IsAny<OrchestratorDecision>(),
            It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public void ClassifyUseCase_DelegatesToInner()
    {
        // Arrange
        _mockInner.Setup(i => i.ClassifyUseCase("test"))
            .Returns(UseCase.Chat);

        // Act
        var result = _sut.ClassifyUseCase("test");

        // Assert
        result.Should().Be(UseCase.Chat);
        _mockInner.Verify(i => i.ClassifyUseCase("test"), Times.Once);
    }

    [Fact]
    public void RegisterModel_DelegatesToInner()
    {
        // Arrange
        var capability = new ModelCapability("model", "provider", 0.8, 100, new List<UseCase> { UseCase.Chat });

        // Act
        _sut.RegisterModel(capability);

        // Assert
        _mockInner.Verify(i => i.RegisterModel(capability), Times.Once);
    }

    [Fact]
    public void RecordMetric_DelegatesToInner()
    {
        // Act
        _sut.RecordMetric("resource", 50.0, true);

        // Assert
        _mockInner.Verify(i => i.RecordMetric("resource", 50.0, true), Times.Once);
    }

    [Fact]
    public void GetMetrics_DelegatesToInner()
    {
        // Arrange
        var metrics = new Dictionary<string, PerformanceMetrics>();
        _mockInner.Setup(i => i.GetMetrics())
            .Returns(metrics);

        // Act
        var result = _sut.GetMetrics();

        // Assert
        result.Should().BeSameAs(metrics);
    }

    [Fact]
    public void GetCacheStatistics_ReturnsFromCache()
    {
        // Arrange
        var stats = new CacheStatistics(10, 100, 50, 20, 0.71, 1024);
        _mockCache.Setup(c => c.GetStatistics()).Returns(stats);

        // Act
        var result = _sut.GetCacheStatistics();

        // Assert
        result.Should().Be(stats);
    }

    [Fact]
    public async Task ClearCacheAsync_DelegatesToCache()
    {
        // Act
        await _sut.ClearCacheAsync();

        // Assert
        _mockCache.Verify(c => c.ClearAsync(), Times.Once);
    }
}

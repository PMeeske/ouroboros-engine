using Ouroboros.Agent.MetaAI;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class UncertaintyRouterTests
{
    private readonly Mock<IModelOrchestrator> _orchestratorMock;

    public UncertaintyRouterTests()
    {
        _orchestratorMock = new Mock<IModelOrchestrator>();
    }

    #region Constructor

    [Fact]
    public void Constructor_WithNullOrchestrator_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new UncertaintyRouter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("orchestrator");
    }

    [Fact]
    public void Constructor_WithValidOrchestrator_ShouldInitialize()
    {
        // Act
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Assert
        router.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithThresholds_ShouldClampValues()
    {
        // Act
        var router = new UncertaintyRouter(_orchestratorMock.Object, minConfidenceThreshold: 1.5, humanOversightThreshold: -0.5);

        // Assert
        router.Should().NotBeNull();
    }

    #endregion

    #region RouteDecisionAsync

    [Fact]
    public async Task RouteDecisionAsync_WithEmptyAction_ShouldReturnNotProceed()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var result = await router.RouteDecisionAsync("context", "", 0.9);

        // Assert
        result.ShouldProceed.Should().BeFalse();
        result.Reason.Should().Contain("empty");
        result.RecommendedStrategy.Should().Be(FallbackStrategy.Abort);
    }

    [Fact]
    public async Task RouteDecisionAsync_HighConfidence_ShouldProceed()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var result = await router.RouteDecisionAsync("context", "safe action", 0.95);

        // Assert
        result.ShouldProceed.Should().BeTrue();
        result.RequiresHumanOversight.Should().BeFalse();
    }

    [Fact]
    public async Task RouteDecisionAsync_LowConfidence_ShouldNotProceed()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var result = await router.RouteDecisionAsync("context", "risky action", 0.3);

        // Assert
        result.ShouldProceed.Should().BeFalse();
        result.AlternativeActions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RouteDecisionAsync_CriticalContext_ShouldRequireOversight()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var result = await router.RouteDecisionAsync("critical decision context", "action", 0.85);

        // Assert
        result.RequiresHumanOversight.Should().BeTrue();
    }

    [Fact]
    public async Task RouteDecisionAsync_HighRiskAction_ShouldRequireOversight()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var result = await router.RouteDecisionAsync("context", "delete all data", 0.6);

        // Assert
        result.RequiresHumanOversight.Should().BeTrue();
    }

    #endregion

    #region RequiresHumanOversightAsync

    [Fact]
    public async Task RequiresHumanOversightAsync_BelowThreshold_ShouldReturnTrue()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object, humanOversightThreshold: 0.7);

        // Act
        var result = await router.RequiresHumanOversightAsync("context", 0.3, 0.5);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequiresHumanOversightAsync_HighRiskLowConfidence_ShouldReturnTrue()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var result = await router.RequiresHumanOversightAsync("context", 0.8, 0.7);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequiresHumanOversightAsync_CriticalContextLowConfidence_ShouldReturnTrue()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var result = await router.RequiresHumanOversightAsync("critical sensitive operation", 0.3, 0.8);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequiresHumanOversightAsync_HighConfidence_ShouldReturnFalse()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var result = await router.RequiresHumanOversightAsync("context", 0.3, 0.95);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetFallbackStrategyAsync

    [Fact]
    public async Task GetFallbackStrategyAsync_VeryLowConfidenceFirstAttempt_ShouldReturnRequestClarification()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var strategy = await router.GetFallbackStrategyAsync(0.2, 0);

        // Assert
        strategy.Should().Be(FallbackStrategy.RequestClarification);
    }

    [Fact]
    public async Task GetFallbackStrategyAsync_VeryLowConfidenceRetry_ShouldReturnAbort()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var strategy = await router.GetFallbackStrategyAsync(0.2, 1);

        // Assert
        strategy.Should().Be(FallbackStrategy.Abort);
    }

    [Fact]
    public async Task GetFallbackStrategyAsync_LowConfidenceFirstAttempt_ShouldReturnUseConservativeApproach()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var strategy = await router.GetFallbackStrategyAsync(0.4, 0);

        // Assert
        strategy.Should().Be(FallbackStrategy.UseConservativeApproach);
    }

    [Fact]
    public async Task GetFallbackStrategyAsync_LowConfidenceRetry_ShouldReturnRetry()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var strategy = await router.GetFallbackStrategyAsync(0.4, 1);

        // Assert
        strategy.Should().Be(FallbackStrategy.Retry);
    }

    [Fact]
    public async Task GetFallbackStrategyAsync_ModerateConfidence_ShouldReturnRetryOrDefer()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var strategy0 = await router.GetFallbackStrategyAsync(0.6, 0);
        var strategy1 = await router.GetFallbackStrategyAsync(0.6, 1);

        // Assert
        strategy0.Should().Be(FallbackStrategy.Retry);
        strategy1.Should().Be(FallbackStrategy.Defer);
    }

    [Fact]
    public async Task GetFallbackStrategyAsync_HighConfidence_ShouldReturnUseConservativeApproach()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var strategy = await router.GetFallbackStrategyAsync(0.9, 0);

        // Assert
        strategy.Should().Be(FallbackStrategy.UseConservativeApproach);
    }

    [Fact]
    public async Task GetFallbackStrategyAsync_ManyAttempts_ShouldReturnAbortOrEscalate()
    {
        // Arrange
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        // Act
        var strategyLow = await router.GetFallbackStrategyAsync(0.2, 5);
        var strategyHigh = await router.GetFallbackStrategyAsync(0.6, 5);

        // Assert
        strategyLow.Should().Be(FallbackStrategy.Abort);
        strategyHigh.Should().Be(FallbackStrategy.EscalateToHuman);
    }

    #endregion
}

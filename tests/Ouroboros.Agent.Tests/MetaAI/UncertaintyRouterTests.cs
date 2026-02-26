// <copyright file="UncertaintyRouterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class UncertaintyRouterTests
{
    private readonly Mock<IModelOrchestrator> _orchestratorMock = new();

    [Fact]
    public void Constructor_NullOrchestrator_Throws()
    {
        var act = () => new UncertaintyRouter(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_DefaultThresholds_DoesNotThrow()
    {
        var act = () => new UncertaintyRouter(_orchestratorMock.Object);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_CustomThresholds_DoesNotThrow()
    {
        var act = () => new UncertaintyRouter(_orchestratorMock.Object, 0.8, 0.3);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task RouteDecisionAsync_EmptyAction_DoesNotProceed()
    {
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        var decision = await router.RouteDecisionAsync("ctx", "", 0.9);

        decision.ShouldProceed.Should().BeFalse();
        decision.Reason.Should().Contain("empty");
    }

    [Fact]
    public async Task RouteDecisionAsync_WhitespaceAction_DoesNotProceed()
    {
        var router = new UncertaintyRouter(_orchestratorMock.Object);

        var decision = await router.RouteDecisionAsync("ctx", "   ", 0.9);

        decision.ShouldProceed.Should().BeFalse();
    }

    [Fact]
    public async Task RouteDecisionAsync_HighConfidence_ShouldProceed()
    {
        var router = new UncertaintyRouter(_orchestratorMock.Object, minConfidenceThreshold: 0.5);

        var decision = await router.RouteDecisionAsync("ctx", "do-something", 0.9);

        decision.ShouldProceed.Should().BeTrue();
        decision.ConfidenceLevel.Should().BeGreaterThanOrEqualTo(0.5);
    }

    [Fact]
    public async Task RouteDecisionAsync_LowConfidence_ShouldNotProceed()
    {
        var router = new UncertaintyRouter(_orchestratorMock.Object, minConfidenceThreshold: 0.9);

        var decision = await router.RouteDecisionAsync("ctx", "do-something", 0.1);

        decision.ShouldProceed.Should().BeFalse();
    }
}

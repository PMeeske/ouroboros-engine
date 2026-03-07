// <copyright file="CostAwareRouterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class CostAwareRouterTests
{
    private readonly Mock<IUncertaintyRouter> _uncertaintyMock = new();
    private readonly Mock<IMetaAIPlannerOrchestrator> _orchestratorMock = new();

    [Fact]
    public void Constructor_NullUncertaintyRouter_Throws()
    {
        var act = () => new CostAwareRouter(null!, _orchestratorMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOrchestrator_Throws()
    {
        var act = () => new CostAwareRouter(_uncertaintyMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidParams_DoesNotThrow()
    {
        var act = () => new CostAwareRouter(_uncertaintyMock.Object, _orchestratorMock.Object);
        act.Should().NotThrow();
    }
}

// <copyright file="MeTTaAgentExtensionsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MeTTaAgents;

namespace Ouroboros.Tests.MeTTaAgents;

[Trait("Category", "Unit")]
public class MeTTaAgentExtensionsTests
{
    [Fact]
    public void CreateDefaultProviders_ReturnsProviders()
    {
        var providers = MeTTaAgentExtensions.CreateDefaultProviders();

        providers.Should().NotBeEmpty();
        providers.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void CreateDefaultRuntime_ReturnsRuntime()
    {
        var engineMock = new Mock<IMeTTaEngine>();

        var runtime = MeTTaAgentExtensions.CreateDefaultRuntime(engineMock.Object);

        runtime.Should().NotBeNull();
        runtime.Engine.Should().BeSameAs(engineMock.Object);
    }
}

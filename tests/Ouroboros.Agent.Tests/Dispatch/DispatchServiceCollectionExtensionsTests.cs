// <copyright file="DispatchServiceCollectionExtensionsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Agent.Dispatch;

namespace Ouroboros.Tests.Dispatch;

[Trait("Category", "Unit")]
public sealed class DispatchServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOuroborosAgentDispatch_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddOuroborosAgentDispatch();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddOuroborosAgentDispatch_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOuroborosAgentDispatch();

        // Assert
        services.Should().NotBeEmpty();
    }
}

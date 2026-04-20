using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.Interop.Hosting;
using Xunit;

namespace Ouroboros.Tests.Hosting;

[Trait("Category", "Unit")]
public sealed class HostStepExtensionsTests
{
    [Fact]
    public void Use_WithHostApplicationBuilder_ReturnsStep()
    {
        // Act
        var step = HostStepExtensions.Use(b => b);

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public void Use_WithConfigurationManager_ReturnsStep()
    {
        // Act
        var step = HostStepExtensions.Use((ConfigurationManager c) => c);

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public void Services_ReturnsStep()
    {
        // Act
        var step = HostStepExtensions.Services(services => { });

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public void AddInterchangeableLlm_ReturnsStep()
    {
        // Act
        var step = HostStepExtensions.AddInterchangeableLlm();

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public void AddInterchangeableLlm_WithCustomModels_ReturnsStep()
    {
        // Act
        var step = HostStepExtensions.AddInterchangeableLlm("llama3", "nomic-embed-text");

        // Assert
        step.Should().NotBeNull();
    }
}

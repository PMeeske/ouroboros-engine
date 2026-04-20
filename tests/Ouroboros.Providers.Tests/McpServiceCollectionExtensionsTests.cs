using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Providers;
using Ouroboros.Providers.Docker;
using Ouroboros.Providers.DuckDuckGo;
using Ouroboros.Providers.Firecrawl;
using Ouroboros.Providers.Kubernetes;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class McpServiceCollectionExtensionsTests
{
    [Fact]
    public void AddKubernetesMcpClient_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        FluentActions.Invoking(() => services.AddKubernetesMcpClient())
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddKubernetesMcpClient_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddKubernetesMcpClient();

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(KubernetesMcpClientOptions));
        services.Should().Contain(sd => sd.ServiceType == typeof(IKubernetesMcpClient));
    }

    [Fact]
    public void AddKubernetesMcpClient_WithConfigure_InvokesAction()
    {
        // Arrange
        var services = new ServiceCollection();
        bool configureInvoked = false;

        // Act
        services.AddKubernetesMcpClient(options => configureInvoked = true);

        // Assert
        configureInvoked.Should().BeTrue();
    }

    [Fact]
    public void AddDockerMcpClient_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        FluentActions.Invoking(() => services.AddDockerMcpClient())
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddDockerMcpClient_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDockerMcpClient();

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(DockerMcpClientOptions));
        services.Should().Contain(sd => sd.ServiceType == typeof(IDockerMcpClient));
    }

    [Fact]
    public void AddDockerMcpClient_WithConfigure_InvokesAction()
    {
        // Arrange
        var services = new ServiceCollection();
        bool configureInvoked = false;

        // Act
        services.AddDockerMcpClient(options => configureInvoked = true);

        // Assert
        configureInvoked.Should().BeTrue();
    }

    [Fact]
    public void AddFirecrawlMcpClient_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        FluentActions.Invoking(() => services.AddFirecrawlMcpClient())
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddFirecrawlMcpClient_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFirecrawlMcpClient();

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(FirecrawlMcpClientOptions));
        services.Should().Contain(sd => sd.ServiceType == typeof(IFirecrawlMcpClient));
    }

    [Fact]
    public void AddDuckDuckGoMcpClient_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        FluentActions.Invoking(() => services.AddDuckDuckGoMcpClient())
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddDuckDuckGoMcpClient_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDuckDuckGoMcpClient();

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(DuckDuckGoMcpClientOptions));
        services.Should().Contain(sd => sd.ServiceType == typeof(IDuckDuckGoMcpClient));
    }

    [Fact]
    public void AddAllMcpClients_RegistersAllFour()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAllMcpClients();

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(IKubernetesMcpClient));
        services.Should().Contain(sd => sd.ServiceType == typeof(IDockerMcpClient));
        services.Should().Contain(sd => sd.ServiceType == typeof(IFirecrawlMcpClient));
        services.Should().Contain(sd => sd.ServiceType == typeof(IDuckDuckGoMcpClient));
    }

    [Fact]
    public void AddAllMcpClients_IsIdempotent()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - register twice
        services.AddAllMcpClients();
        services.AddAllMcpClients();

        // Assert - TryAddSingleton should prevent duplicate registrations
        var kubeRegistrations = services.Count(sd => sd.ServiceType == typeof(IKubernetesMcpClient));
        kubeRegistrations.Should().Be(1);
    }
}

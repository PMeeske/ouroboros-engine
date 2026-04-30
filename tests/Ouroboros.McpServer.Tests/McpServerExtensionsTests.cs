using Microsoft.Extensions.DependencyInjection;

namespace Ouroboros.McpServer.Tests;

[Trait("Category", "Unit")]
public class McpServerExtensionsTests
{
    [Fact]
    public void AddOuroborosMcpServer_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var act = () => services.AddOuroborosMcpServer();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddOuroborosMcpServer_RegistersOptionsAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ToolRegistry());

        services.AddOuroborosMcpServer();

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<McpServerOptions>();
        options.Should().NotBeNull();
        options!.ServerName.Should().Be("Ouroboros");
    }

    [Fact]
    public void AddOuroborosMcpServer_WithConfigure_AppliesConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ToolRegistry());

        services.AddOuroborosMcpServer(opt =>
        {
            opt.ServerName = "Custom";
            opt.ServerVersion = "9.9.9";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<McpServerOptions>();
        options!.ServerName.Should().Be("Custom");
        options!.ServerVersion.Should().Be("9.9.9");
    }

    [Fact]
    public void AddOuroborosMcpServer_RegistersMcpServerAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ToolRegistry());

        services.AddOuroborosMcpServer();

        var provider = services.BuildServiceProvider();
        var server = provider.GetService<OuroborosMcpServer>();
        server.Should().NotBeNull();
    }

    [Fact]
    public void AddOuroborosMcpServer_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddOuroborosMcpServer();

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddOuroborosMcpServer_CalledTwice_DoesNotDuplicate()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ToolRegistry());

        services.AddOuroborosMcpServer();
        services.AddOuroborosMcpServer(opt => opt.ServerName = "Second");

        // TryAddSingleton should keep the first registration
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<McpServerOptions>();
        options!.ServerName.Should().Be("Ouroboros");
    }
}

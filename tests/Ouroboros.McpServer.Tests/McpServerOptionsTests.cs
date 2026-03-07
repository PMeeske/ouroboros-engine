using FluentAssertions;
using Xunit;

namespace Ouroboros.McpServer.Tests;

[Trait("Category", "Unit")]
public class McpServerOptionsTests
{
    [Fact]
    public void DefaultServerName_IsOuroboros()
    {
        var options = new McpServerOptions();

        options.ServerName.Should().Be("Ouroboros");
    }

    [Fact]
    public void DefaultServerVersion_Is1_0_0()
    {
        var options = new McpServerOptions();

        options.ServerVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void DefaultTransport_IsStdio()
    {
        var options = new McpServerOptions();

        options.Transport.Should().Be(McpTransport.Stdio);
    }

    [Fact]
    public void DefaultToolFilter_IsNull()
    {
        var options = new McpServerOptions();

        options.ToolFilter.Should().BeNull();
    }

    [Fact]
    public void ServerName_CanBeSet()
    {
        var options = new McpServerOptions { ServerName = "Custom" };

        options.ServerName.Should().Be("Custom");
    }

    [Fact]
    public void ServerVersion_CanBeSet()
    {
        var options = new McpServerOptions { ServerVersion = "2.0.0" };

        options.ServerVersion.Should().Be("2.0.0");
    }

    [Fact]
    public void Transport_CanBeSetToSse()
    {
        var options = new McpServerOptions { Transport = McpTransport.Sse };

        options.Transport.Should().Be(McpTransport.Sse);
    }

    [Fact]
    public void ToolFilter_CanBeSet()
    {
        var options = new McpServerOptions { ToolFilter = ["tool1", "tool2"] };

        options.ToolFilter.Should().BeEquivalentTo(new[] { "tool1", "tool2" });
    }
}

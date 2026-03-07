using Ouroboros.Providers.Docker;

namespace Ouroboros.Tests.Docker;

[Trait("Category", "Unit")]
public sealed class DockerMcpClientOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new DockerMcpClientOptions();

        options.BaseUrl.Should().BeNull();
        options.SocketPath.Should().Be("/var/run/docker.sock");
        options.PipePath.Should().Be(@"//./pipe/docker_engine");
        options.ApiVersion.Should().Be("v1.43");
        options.UseTls.Should().BeFalse();
        options.TlsCertPath.Should().BeNull();
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void IsValid_WithExplicitBaseUrl_ReturnsTrue()
    {
        var options = new DockerMcpClientOptions { BaseUrl = "http://localhost:2375" };
        options.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_OnWindows_ReturnsTrue()
    {
        // On Windows (where these tests run), this should be true
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            var options = new DockerMcpClientOptions();
            options.IsValid().Should().BeTrue();
        }
    }
}

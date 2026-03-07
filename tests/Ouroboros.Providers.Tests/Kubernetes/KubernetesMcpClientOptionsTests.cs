using Ouroboros.Providers.Kubernetes;

namespace Ouroboros.Tests.Kubernetes;

[Trait("Category", "Unit")]
public sealed class KubernetesMcpClientOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new KubernetesMcpClientOptions();

        options.BaseUrl.Should().BeNull();
        options.Token.Should().BeNull();
        options.KubeConfigPath.Should().BeNull();
        options.Context.Should().BeNull();
        options.SkipTlsVerify.Should().BeFalse();
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void IsValid_WithBaseUrlAndToken_ReturnsTrue()
    {
        var options = new KubernetesMcpClientOptions
        {
            BaseUrl = "https://localhost:6443",
            Token = "my-token"
        };

        options.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithKubeConfigPath_ReturnsTrue()
    {
        var options = new KubernetesMcpClientOptions
        {
            KubeConfigPath = "/some/path"
        };

        options.IsValid().Should().BeTrue();
    }
}

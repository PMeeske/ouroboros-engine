using Ouroboros.Providers.Docker;

namespace Ouroboros.Tests.Docker;

[Trait("Category", "Unit")]
public sealed class DockerModelsTests
{
    [Fact]
    public void ContainerInfo_ShortId_TruncatesTo12()
    {
        var container = new DockerContainerInfo
        {
            Id = "abcdef1234567890abcdef",
            Image = "nginx",
            State = "running"
        };

        container.ShortId.Should().Be("abcdef123456");
    }

    [Fact]
    public void ContainerInfo_ShortId_ShortId_ReturnsAsIs()
    {
        var container = new DockerContainerInfo
        {
            Id = "short",
            Image = "nginx",
            State = "running"
        };

        container.ShortId.Should().Be("short");
    }

    [Fact]
    public void ContainerInfo_DefaultCollections()
    {
        var container = new DockerContainerInfo { Id = "1", Image = "img", State = "r" };

        container.Names.Should().BeEmpty();
        container.Ports.Should().BeEmpty();
        container.Labels.Should().BeEmpty();
    }

    [Fact]
    public void PortMapping_Defaults()
    {
        var port = new DockerPortMapping { ContainerPort = 80 };

        port.Protocol.Should().Be("tcp");
        port.HostIp.Should().BeNull();
        port.HostPort.Should().BeNull();
    }

    [Fact]
    public void ImageInfo_Defaults()
    {
        var image = new DockerImageInfo { Id = "img1" };

        image.RepoTags.Should().BeEmpty();
        image.Size.Should().Be(0);
    }

    [Fact]
    public void NetworkInfo_SetsProperties()
    {
        var network = new DockerNetworkInfo { Id = "n1", Name = "bridge", Driver = "bridge" };

        network.Id.Should().Be("n1");
        network.Name.Should().Be("bridge");
        network.Driver.Should().Be("bridge");
        network.Scope.Should().BeNull();
    }

    [Fact]
    public void VolumeInfo_Defaults()
    {
        var volume = new DockerVolumeInfo { Name = "v1", Driver = "local" };

        volume.Mountpoint.Should().BeNull();
        volume.Labels.Should().BeEmpty();
    }
}

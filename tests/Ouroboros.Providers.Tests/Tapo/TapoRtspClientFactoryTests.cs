using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoRtspClientFactoryTests : IDisposable
{
    private readonly TapoRtspClientFactory _sut;

    public TapoRtspClientFactoryTests()
    {
        var cameras = new List<TapoDevice>
        {
            new() { Name = "front-door", DeviceType = TapoDeviceType.C200, IpAddress = "192.168.1.10" },
            new() { Name = "backyard", DeviceType = TapoDeviceType.C210, IpAddress = "192.168.1.11" }
        };
        _sut = new TapoRtspClientFactory(cameras, "user", "pass");
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    [Fact]
    public void GetCameraNames_ShouldReturnAllConfiguredCameras()
    {
        // Act
        var names = _sut.GetCameraNames().ToList();

        // Assert
        names.Should().HaveCount(2);
        names.Should().Contain("front-door");
        names.Should().Contain("backyard");
    }

    [Fact]
    public void GetClient_WithValidName_ShouldReturnClient()
    {
        // Act
        var client = _sut.GetClient("front-door");

        // Assert
        client.Should().NotBeNull();
        client!.CameraIp.Should().Be("192.168.1.10");
    }

    [Fact]
    public void GetClient_WithInvalidName_ShouldReturnNull()
    {
        // Act
        var client = _sut.GetClient("nonexistent");

        // Assert
        client.Should().BeNull();
    }

    [Fact]
    public void GetClient_ShouldBeCaseInsensitive()
    {
        // Act
        var client = _sut.GetClient("FRONT-DOOR");

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ShouldClearClients()
    {
        // Act
        _sut.Dispose();

        // Assert
        _sut.GetCameraNames().Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyCameraList_ShouldCreateEmptyFactory()
    {
        // Arrange & Act
        using var factory = new TapoRtspClientFactory(
            Array.Empty<TapoDevice>(), "user", "pass");

        // Assert
        factory.GetCameraNames().Should().BeEmpty();
    }
}

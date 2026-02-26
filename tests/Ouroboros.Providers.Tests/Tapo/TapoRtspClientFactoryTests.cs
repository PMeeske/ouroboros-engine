namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class TapoRtspClientFactoryTests
{
    [Fact]
    public void GetClient_ExistingCamera_ReturnsClient()
    {
        var cameras = new[]
        {
            new TapoDevice { Name = "cam1", DeviceType = TapoDeviceType.C200, IpAddress = "10.0.0.1" },
            new TapoDevice { Name = "cam2", DeviceType = TapoDeviceType.C210, IpAddress = "10.0.0.2" }
        };

        using var factory = new TapoRtspClientFactory(cameras, "user", "pass");

        var client = factory.GetClient("cam1");
        client.Should().NotBeNull();
        client!.CameraIp.Should().Be("10.0.0.1");
    }

    [Fact]
    public void GetClient_NonExistingCamera_ReturnsNull()
    {
        using var factory = new TapoRtspClientFactory(
            Array.Empty<TapoDevice>(), "user", "pass");

        factory.GetClient("missing").Should().BeNull();
    }

    [Fact]
    public void GetClient_CaseInsensitiveLookup()
    {
        var cameras = new[]
        {
            new TapoDevice { Name = "MyCam", DeviceType = TapoDeviceType.C200, IpAddress = "10.0.0.1" }
        };

        using var factory = new TapoRtspClientFactory(cameras, "user", "pass");

        factory.GetClient("mycam").Should().NotBeNull();
    }

    [Fact]
    public void GetCameraNames_ReturnsAllNames()
    {
        var cameras = new[]
        {
            new TapoDevice { Name = "a", DeviceType = TapoDeviceType.C200, IpAddress = "1.1.1.1" },
            new TapoDevice { Name = "b", DeviceType = TapoDeviceType.C210, IpAddress = "2.2.2.2" }
        };

        using var factory = new TapoRtspClientFactory(cameras, "user", "pass");

        factory.GetCameraNames().Should().Contain(new[] { "a", "b" });
    }

    [Fact]
    public void Dispose_ClearsClients()
    {
        var cameras = new[]
        {
            new TapoDevice { Name = "cam", DeviceType = TapoDeviceType.C200, IpAddress = "1.1.1.1" }
        };

        var factory = new TapoRtspClientFactory(cameras, "user", "pass");
        factory.Dispose();

        factory.GetCameraNames().Should().BeEmpty();
    }
}

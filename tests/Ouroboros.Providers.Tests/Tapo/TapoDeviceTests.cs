namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class TapoDeviceTests
{
    [Fact]
    public void Ctor_SetsProperties()
    {
        var device = new TapoDevice
        {
            Name = "living-room",
            DeviceType = TapoDeviceType.L530,
            IpAddress = "192.168.1.100"
        };

        device.Name.Should().Be("living-room");
        device.DeviceType.Should().Be(TapoDeviceType.L530);
        device.IpAddress.Should().Be("192.168.1.100");
    }

    [Fact]
    public void RoundTrips_ThroughJson()
    {
        var original = new TapoDevice
        {
            Name = "plug-1",
            DeviceType = TapoDeviceType.P100,
            IpAddress = "10.0.0.1"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<TapoDevice>(json);

        deserialized.Should().Be(original);
    }
}

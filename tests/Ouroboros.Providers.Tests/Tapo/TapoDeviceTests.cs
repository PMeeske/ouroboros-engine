using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoDeviceTests
{
    [Fact]
    public void TapoDevice_Construction_ShouldSetAllProperties()
    {
        // Arrange & Act
        var device = new TapoDevice
        {
            Name = "living-room-bulb",
            DeviceType = TapoDeviceType.L530,
            IpAddress = "192.168.1.100"
        };

        // Assert
        device.Name.Should().Be("living-room-bulb");
        device.DeviceType.Should().Be(TapoDeviceType.L530);
        device.IpAddress.Should().Be("192.168.1.100");
    }

    [Fact]
    public void TapoDevice_Equality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var a = new TapoDevice
        {
            Name = "plug1",
            DeviceType = TapoDeviceType.P100,
            IpAddress = "10.0.0.1"
        };
        var b = new TapoDevice
        {
            Name = "plug1",
            DeviceType = TapoDeviceType.P100,
            IpAddress = "10.0.0.1"
        };

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void TapoDevice_Equality_DifferentNames_ShouldNotBeEqual()
    {
        // Arrange
        var a = new TapoDevice
        {
            Name = "plug1",
            DeviceType = TapoDeviceType.P100,
            IpAddress = "10.0.0.1"
        };
        var b = new TapoDevice
        {
            Name = "plug2",
            DeviceType = TapoDeviceType.P100,
            IpAddress = "10.0.0.1"
        };

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void TapoDevice_JsonSerialization_ShouldUseJsonPropertyNames()
    {
        // Arrange
        var device = new TapoDevice
        {
            Name = "test-device",
            DeviceType = TapoDeviceType.C200,
            IpAddress = "192.168.1.50"
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(device);

        // Assert
        json.Should().Contain("\"name\":");
        json.Should().Contain("\"device_type\":");
        json.Should().Contain("\"ip_addr\":");
    }

    [Fact]
    public void TapoDevice_JsonDeserialization_ShouldRoundtrip()
    {
        // Arrange
        var original = new TapoDevice
        {
            Name = "camera1",
            DeviceType = TapoDeviceType.C200,
            IpAddress = "192.168.1.10"
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TapoDevice>(json);

        // Assert
        deserialized.Should().Be(original);
    }

    [Fact]
    public void TapoDevice_With_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new TapoDevice
        {
            Name = "bulb1",
            DeviceType = TapoDeviceType.L510,
            IpAddress = "192.168.1.1"
        };

        // Act
        var modified = original with { IpAddress = "192.168.1.2" };

        // Assert
        modified.IpAddress.Should().Be("192.168.1.2");
        original.IpAddress.Should().Be("192.168.1.1");
    }
}

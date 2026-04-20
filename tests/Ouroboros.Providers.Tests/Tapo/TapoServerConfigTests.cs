using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoServerConfigTests
{
    [Fact]
    public void TapoServerConfig_Construction_ShouldSetAllProperties()
    {
        // Arrange
        var credentials = new TapoCredentials
        {
            Email = "user@example.com",
            Password = "tapo_pass"
        };
        var devices = new List<TapoDevice>
        {
            new() { Name = "bulb1", DeviceType = TapoDeviceType.L530, IpAddress = "192.168.1.100" }
        };

        // Act
        var config = new TapoServerConfig
        {
            TapoCredentials = credentials,
            ServerPassword = "server_pass",
            Devices = devices
        };

        // Assert
        config.TapoCredentials.Should().Be(credentials);
        config.ServerPassword.Should().Be("server_pass");
        config.Devices.Should().HaveCount(1);
        config.Devices[0].Name.Should().Be("bulb1");
    }

    [Fact]
    public void TapoServerConfig_EmptyDeviceList_ShouldBeAllowed()
    {
        // Arrange & Act
        var config = new TapoServerConfig
        {
            TapoCredentials = new TapoCredentials { Email = "a@b.com", Password = "p" },
            ServerPassword = "sp",
            Devices = new List<TapoDevice>()
        };

        // Assert
        config.Devices.Should().BeEmpty();
    }

    [Fact]
    public void TapoServerConfig_MultipleDevices_ShouldContainAll()
    {
        // Arrange
        var devices = new List<TapoDevice>
        {
            new() { Name = "bulb1", DeviceType = TapoDeviceType.L530, IpAddress = "192.168.1.1" },
            new() { Name = "plug1", DeviceType = TapoDeviceType.P100, IpAddress = "192.168.1.2" },
            new() { Name = "cam1", DeviceType = TapoDeviceType.C200, IpAddress = "192.168.1.3" }
        };

        // Act
        var config = new TapoServerConfig
        {
            TapoCredentials = new TapoCredentials { Email = "a@b.com", Password = "p" },
            ServerPassword = "sp",
            Devices = devices
        };

        // Assert
        config.Devices.Should().HaveCount(3);
    }

    [Fact]
    public void TapoServerConfig_JsonSerialization_ShouldUseJsonPropertyNames()
    {
        // Arrange
        var config = new TapoServerConfig
        {
            TapoCredentials = new TapoCredentials { Email = "a@b.com", Password = "p" },
            ServerPassword = "sp",
            Devices = new List<TapoDevice>()
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(config);

        // Assert
        json.Should().Contain("\"tapo_credentials\":");
        json.Should().Contain("\"server_password\":");
        json.Should().Contain("\"devices\":");
    }

    [Fact]
    public void TapoServerConfig_JsonDeserialization_ShouldRoundtrip()
    {
        // Arrange
        var original = new TapoServerConfig
        {
            TapoCredentials = new TapoCredentials { Email = "test@example.com", Password = "pass" },
            ServerPassword = "server_pass",
            Devices = new List<TapoDevice>
            {
                new() { Name = "bulb", DeviceType = TapoDeviceType.L510, IpAddress = "10.0.0.1" }
            }
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TapoServerConfig>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ServerPassword.Should().Be("server_pass");
        deserialized.TapoCredentials.Email.Should().Be("test@example.com");
        deserialized.Devices.Should().HaveCount(1);
    }
}

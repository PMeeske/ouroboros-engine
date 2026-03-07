namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class TapoServerConfigTests
{
    [Fact]
    public void Ctor_SetsProperties()
    {
        var creds = new TapoCredentials { Email = "e@x.com", Password = "pw" };
        var devices = new List<TapoDevice>
        {
            new() { Name = "d1", DeviceType = TapoDeviceType.L510, IpAddress = "1.2.3.4" }
        };

        var config = new TapoServerConfig
        {
            TapoCredentials = creds,
            ServerPassword = "srv-pw",
            Devices = devices
        };

        config.TapoCredentials.Should().Be(creds);
        config.ServerPassword.Should().Be("srv-pw");
        config.Devices.Should().HaveCount(1);
    }

    [Fact]
    public void RoundTrips_ThroughJson()
    {
        var config = new TapoServerConfig
        {
            TapoCredentials = new TapoCredentials { Email = "a@b.com", Password = "p" },
            ServerPassword = "s",
            Devices = new List<TapoDevice>()
        };

        var json = JsonSerializer.Serialize(config);
        var deserialized = JsonSerializer.Deserialize<TapoServerConfig>(json);

        deserialized!.ServerPassword.Should().Be("s");
        deserialized.TapoCredentials.Email.Should().Be("a@b.com");
    }
}

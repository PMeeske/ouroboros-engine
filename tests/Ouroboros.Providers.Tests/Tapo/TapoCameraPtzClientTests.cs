namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class TapoCameraPtzClientTests
{
    [Fact]
    public void Ctor_SetsProperties()
    {
        using var client = new TapoCameraPtzClient("192.168.1.50", "admin", "password123");

        client.CameraIp.Should().Be("192.168.1.50");
        client.Capabilities.Should().Be(PtzCapabilities.Default);
    }

    [Fact]
    public void Ctor_NullCameraIp_Throws()
    {
        FluentActions.Invoking(() => new TapoCameraPtzClient(null!, "user", "pass"))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullUsername_Throws()
    {
        FluentActions.Invoking(() => new TapoCameraPtzClient("1.1.1.1", null!, "pass"))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullPassword_Throws()
    {
        FluentActions.Invoking(() => new TapoCameraPtzClient("1.1.1.1", "user", null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var client = new TapoCameraPtzClient("1.1.1.1", "user", "pass");
        client.Dispose();
        client.Dispose(); // Should not throw
    }
}

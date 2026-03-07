namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class TapoRtspClientPropertyTests
{
    [Fact]
    public void RtspUrl_Low_UsesStream2()
    {
        using var client = new TapoRtspClient("192.168.1.1", "user", "pass", CameraStreamQuality.Low);
        client.RtspUrl.Should().Contain("stream2");
    }

    [Fact]
    public void RtspUrl_HD_UsesStream1()
    {
        using var client = new TapoRtspClient("192.168.1.1", "user", "pass", CameraStreamQuality.HD);
        client.RtspUrl.Should().Contain("stream1");
    }

    [Fact]
    public void RtspUrl_ContainsCameraIp()
    {
        using var client = new TapoRtspClient("10.0.0.5", "user", "pass");
        client.RtspUrl.Should().Contain("10.0.0.5");
    }

    [Fact]
    public void CameraIp_ReturnsInitializedValue()
    {
        using var client = new TapoRtspClient("192.168.1.1", "user", "pass");
        client.CameraIp.Should().Be("192.168.1.1");
    }

    [Fact]
    public void FrameCount_StartsAtZero()
    {
        using var client = new TapoRtspClient("192.168.1.1", "user", "pass");
        client.FrameCount.Should().Be(0);
    }

    [Fact]
    public void Ctor_NullCameraIp_Throws()
    {
        FluentActions.Invoking(() => new TapoRtspClient(null!, "user", "pass"))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullUsername_Throws()
    {
        FluentActions.Invoking(() => new TapoRtspClient("1.1.1.1", null!, "pass"))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullPassword_Throws()
    {
        FluentActions.Invoking(() => new TapoRtspClient("1.1.1.1", "user", null!))
            .Should().Throw<ArgumentNullException>();
    }
}

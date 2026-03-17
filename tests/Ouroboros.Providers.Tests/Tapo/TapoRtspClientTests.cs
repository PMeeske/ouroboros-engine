using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoRtspClientTests
{
    [Fact]
    public void Constructor_WithNullCameraIp_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TapoRtspClient(null!, "user", "pass");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUsername_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TapoRtspClient("192.168.1.1", null!, "pass");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPassword_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TapoRtspClient("192.168.1.1", "user", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidParams_ShouldSetProperties()
    {
        // Act
        using var client = new TapoRtspClient("192.168.1.100", "admin", "secret");

        // Assert
        client.CameraIp.Should().Be("192.168.1.100");
        client.FrameCount.Should().Be(0);
    }

    [Fact]
    public void RtspUrl_WithHDQuality_ShouldUseStream1()
    {
        // Arrange
        using var client = new TapoRtspClient("192.168.1.100", "admin", "secret", CameraStreamQuality.HD);

        // Act
        var url = client.RtspUrl;

        // Assert
        url.Should().Contain("stream1");
        url.Should().Contain("192.168.1.100");
    }

    [Fact]
    public void RtspUrl_WithLowQuality_ShouldUseStream2()
    {
        // Arrange
        using var client = new TapoRtspClient("192.168.1.100", "admin", "secret", CameraStreamQuality.Low);

        // Act
        var url = client.RtspUrl;

        // Assert
        url.Should().Contain("stream2");
    }

    [Fact]
    public void RtspUrl_WithStandardQuality_ShouldUseStream2()
    {
        // Arrange
        using var client = new TapoRtspClient("192.168.1.100", "admin", "secret", CameraStreamQuality.Standard);

        // Act
        var url = client.RtspUrl;

        // Assert
        url.Should().Contain("stream2");
    }

    [Theory]
    [InlineData(CameraStreamQuality.FullHD)]
    [InlineData(CameraStreamQuality.QHD)]
    public void RtspUrl_WithHighQuality_ShouldUseStream1(CameraStreamQuality quality)
    {
        // Arrange
        using var client = new TapoRtspClient("192.168.1.100", "admin", "secret", quality);

        // Act
        var url = client.RtspUrl;

        // Assert
        url.Should().Contain("stream1");
    }

    [Fact]
    public void RtspUrl_ShouldEncodeCredentials()
    {
        // Arrange
        using var client = new TapoRtspClient("192.168.1.100", "user@email.com", "p@ss w0rd!");

        // Act
        var url = client.RtspUrl;

        // Assert
        url.Should().StartWith("rtsp://");
        url.Should().Contain("192.168.1.100:554");
        // Password should be URL-encoded
        url.Should().Contain(Uri.EscapeDataString("p@ss w0rd!"));
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var client = new TapoRtspClient("192.168.1.100", "admin", "secret");
        client.Dispose();

        // Act
        var result = await client.CaptureFrameAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public void StopStreaming_WhenNotStreaming_ShouldNotThrow()
    {
        // Arrange
        using var client = new TapoRtspClient("192.168.1.100", "admin", "secret");

        // Act & Assert - should not throw
        client.StopStreaming();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        var client = new TapoRtspClient("192.168.1.100", "admin", "secret");

        // Act & Assert - double dispose should not throw
        client.Dispose();
        client.Dispose();
    }

    [Fact]
    public void FrameCount_Initially_ShouldBeZero()
    {
        // Arrange
        using var client = new TapoRtspClient("192.168.1.100", "admin", "secret");

        // Act & Assert
        client.FrameCount.Should().Be(0);
    }
}

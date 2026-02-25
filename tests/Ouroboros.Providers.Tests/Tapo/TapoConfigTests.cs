namespace Ouroboros.Tests.Providers.Tapo;

/// <summary>
/// Tests for TapoCameraConfig and TapoVoiceOutputConfig.
/// </summary>
[Trait("Category", "Unit")]
public class TapoConfigTests
{
    [Fact]
    public void TapoCameraConfig_HasCorrectDefaults()
    {
        // Act
        var config = new TapoCameraConfig("test-camera");

        // Assert
        config.CameraName.Should().Be("test-camera");
        config.StreamQuality.Should().Be(CameraStreamQuality.HD);
        config.EnableAudio.Should().BeTrue();
        config.EnableMotionDetection.Should().BeTrue();
        config.EnablePersonDetection.Should().BeTrue();
        config.FrameRate.Should().Be(15);
        config.VisionModel.Should().Be("llava:13b");
    }

    [Fact]
    public void TapoVoiceOutputConfig_HasCorrectDefaults()
    {
        // Act
        var config = new TapoVoiceOutputConfig("test-speaker");

        // Assert
        config.DeviceName.Should().Be("test-speaker");
        config.Volume.Should().Be(75);
        config.SampleRate.Should().Be(16000);
    }
}
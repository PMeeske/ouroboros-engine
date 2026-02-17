namespace Ouroboros.Tests.EmbodiedInteraction;

/// <summary>
/// Tests for sensor configurations.
/// </summary>
[Trait("Category", "Unit")]
public class SensorConfigTests
{
    [Fact]
    public void AudioSensorConfig_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new AudioSensorConfig();

        // Assert
        Assert.Equal(16000, config.SampleRate);
        Assert.Equal(1, config.Channels);
        Assert.True(config.EnableVAD);
        Assert.True(config.EnableInterimResults);
    }

    [Fact]
    public void VisualSensorConfig_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new VisualSensorConfig();

        // Assert
        Assert.Equal(640, config.Width);
        Assert.Equal(480, config.Height);
        Assert.Equal(30, config.FrameRate);
        Assert.True(config.EnableObjectDetection);
        Assert.True(config.EnableFaceDetection);
    }

    [Fact]
    public void VoiceConfig_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new VoiceConfig();

        // Assert
        Assert.Equal("default", config.Voice);
        Assert.Equal(1.0, config.Speed);
        Assert.Equal(1.0, config.Pitch);
        Assert.Equal(1.0, config.Volume);
        Assert.Equal("en-US", config.Language);
    }
}
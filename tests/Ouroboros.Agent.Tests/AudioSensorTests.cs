namespace Ouroboros.Tests.EmbodiedInteraction;

/// <summary>
/// Tests for AudioSensor.
/// </summary>
[Trait("Category", "Unit")]
public class AudioSensorTests
{
    [Fact]
    public void AudioSensor_InitializesWithDefaults()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var sttModel = new MockSttModel();
        var config = new AudioSensorConfig();

        // Act
        using var sensor = new AudioSensor(sttModel, self, config);

        // Assert
        Assert.Equal("MockSTT", sensor.ModelName);
        Assert.False(sensor.IsListening);
    }
}
namespace Ouroboros.Tests.EmbodiedInteraction;

/// <summary>
/// Tests for VisualSensor.
/// </summary>
[Trait("Category", "Unit")]
public class VisualSensorTests
{
    [Fact]
    public void VisualSensor_InitializesWithDefaults()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var visionModel = new MockVisionModel();
        var config = new VisualSensorConfig();

        // Act
        using var sensor = new VisualSensor(visionModel, self, config);

        // Assert
        Assert.Equal("MockVision", sensor.ModelName);
        Assert.False(sensor.IsObserving);
    }

    [Fact]
    public void VisualSensor_StartObserving_Succeeds()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var visionModel = new MockVisionModel();
        var config = new VisualSensorConfig();
        using var sensor = new VisualSensor(visionModel, self, config);

        // Act
        var result = sensor.StartObserving();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(sensor.IsObserving);
    }

    [Fact]
    public void VisualSensor_StopObserving_Succeeds()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var visionModel = new MockVisionModel();
        var config = new VisualSensorConfig();
        using var sensor = new VisualSensor(visionModel, self, config);
        sensor.StartObserving();

        // Act
        var result = sensor.StopObserving();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(sensor.IsObserving);
    }

    [Fact]
    public void VisualSensor_FocusOn_UpdatesVirtualSelfAttention()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var visionModel = new MockVisionModel();
        var config = new VisualSensorConfig();
        using var sensor = new VisualSensor(visionModel, self, config);

        // Act
        sensor.FocusOn("user_face");

        // Assert
        Assert.NotNull(self.CurrentState.AttentionFocus);
        Assert.Equal("user_face", self.CurrentState.AttentionFocus.Target);
    }
}
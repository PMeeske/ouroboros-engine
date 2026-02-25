// <copyright file="EmbodimentControllerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.EmbodiedInteraction;

using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for EmbodimentController integration.
/// </summary>
[Trait("Category", "Unit")]
public class EmbodimentControllerTests
{
    [Fact]
    public void EmbodimentController_InitializesWithVirtualSelf()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();

        // Act
        using var controller = new EmbodimentController(self, schema);

        // Assert
        Assert.Same(self, controller.VirtualSelf);
        Assert.Same(schema, controller.BodySchema);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public void EmbodimentController_RegisterAudioSensor_ReturnsSelf()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        using var controller = new EmbodimentController(self, schema);
        var sttModel = new MockSttModel();
        var config = new AudioSensorConfig();
        using var sensor = new AudioSensor(sttModel, self, config);

        // Act
        var result = controller.RegisterAudioSensor("mic1", sensor);

        // Assert
        Assert.Same(controller, result); // Fluent interface
    }

    [Fact]
    public void EmbodimentController_RegisterVisualSensor_ReturnsSelf()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        using var controller = new EmbodimentController(self, schema);
        var visionModel = new MockVisionModel();
        var config = new VisualSensorConfig();
        using var sensor = new VisualSensor(visionModel, self, config);

        // Act
        var result = controller.RegisterVisualSensor("cam1", sensor);

        // Assert
        Assert.Same(controller, result); // Fluent interface
    }

    [Fact]
    public void EmbodimentController_RegisterVoiceActuator_ReturnsSelf()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        using var controller = new EmbodimentController(self, schema);
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        var result = controller.RegisterVoiceActuator("voice1", actuator);

        // Assert
        Assert.Same(controller, result); // Fluent interface
    }

    [Fact]
    public async Task EmbodimentController_OnTextInput_EmitsPerception()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateConversational();
        using var controller = new EmbodimentController(self, schema);

        var receivedPerceptions = new List<UnifiedPerception>();
        using var sub = controller.Perceptions.Subscribe(p => receivedPerceptions.Add(p));

        // Act
        controller.OnTextInput("Hello, AI!", "user");
        await Task.Delay(50); // Allow Rx propagation

        // Assert
        Assert.Single(receivedPerceptions);
        Assert.Equal(SensorModality.Text, receivedPerceptions[0].Modality);
        var textPerception = receivedPerceptions[0].Perception as TextPerception;
        Assert.NotNull(textPerception);
        Assert.Equal("Hello, AI!", textPerception.Text);
    }

    [Fact]
    public async Task EmbodimentController_SpeakAsync_WithNoActuator_ReturnsError()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateConversational();
        using var controller = new EmbodimentController(self, schema);

        // Act
        var result = await controller.SpeakAsync("Hello");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("No voice actuator", result.Error);
    }

    [Fact]
    public async Task EmbodimentController_SpeakAsync_WithActuator_Succeeds()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        using var controller = new EmbodimentController(self, schema);

        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);
        controller.RegisterVoiceActuator("voice1", actuator);

        // Act
        var result = await controller.SpeakAsync("Hello, world!");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello, world!", result.Value.Text);
    }

    [Fact]
    public async Task EmbodimentController_ExecuteActionAsync_VoiceAction()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        using var controller = new EmbodimentController(self, schema);

        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);
        controller.RegisterVoiceActuator("voice1", actuator);

        var request = new ActionRequest(
            "voice1",
            ActuatorModality.Voice,
            "Testing speech output");

        // Act
        var result = await controller.ExecuteActionAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void EmbodimentController_Dispose_DisposesComponents()
    {
        // Arrange
        var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        var controller = new EmbodimentController(self, schema);

        var ttsModel = new MockTtsModel();
        var actuator = new VoiceActuator(ttsModel, self);
        controller.RegisterVoiceActuator("voice1", actuator);

        // Act
        controller.Dispose();

        // Assert - attempting to subscribe to disposed stream should throw
        Assert.Throws<ObjectDisposedException>(() =>
        {
            controller.Perceptions.Subscribe(_ => { });
        });
    }
}
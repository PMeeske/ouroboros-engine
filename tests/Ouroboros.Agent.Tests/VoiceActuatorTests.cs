namespace Ouroboros.Tests.EmbodiedInteraction;

/// <summary>
/// Tests for VoiceActuator.
/// </summary>
[Trait("Category", "Unit")]
public class VoiceActuatorTests
{
    [Fact]
    public void VoiceActuator_InitializesWithDefaults()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();

        // Act
        using var actuator = new VoiceActuator(ttsModel, self);

        // Assert
        Assert.Equal("MockTTS", actuator.ModelName);
        Assert.False(actuator.IsSpeaking);
        Assert.Equal(1.0, actuator.Config.Speed);
    }

    [Fact]
    public void VoiceActuator_Configure_UpdatesConfig()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        var newConfig = new VoiceConfig(
            Voice: "custom-voice",
            Speed: 1.5,
            Pitch: 1.2);

        // Act
        actuator.Configure(newConfig);

        // Assert
        Assert.Equal("custom-voice", actuator.Config.Voice);
        Assert.Equal(1.5, actuator.Config.Speed);
        Assert.Equal(1.2, actuator.Config.Pitch);
    }

    [Fact]
    public void VoiceActuator_SetSpeed_ClampsValue()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        actuator.SetSpeed(3.0); // Above max

        // Assert
        Assert.Equal(2.0, actuator.Config.Speed); // Clamped to max
    }

    [Fact]
    public async Task VoiceActuator_SpeakAsync_SynthesizesSpeech()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        var result = await actuator.SpeakAsync("Hello, world!");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello, world!", result.Value.Text);
        Assert.NotEmpty(result.Value.AudioData);
    }

    [Fact]
    public async Task VoiceActuator_SpeakAsync_EmptyText_ReturnsError()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        var result = await actuator.SpeakAsync("");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.Error);
    }

    [Fact]
    public async Task VoiceActuator_SpeakAsync_EmitsSpeechOutput()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        var receivedSpeech = new List<SynthesizedSpeech>();
        using var sub = actuator.SpeechOutput.Subscribe(s => receivedSpeech.Add(s));

        // Act
        await actuator.SpeakAsync("Test message");
        await Task.Delay(50); // Allow Rx propagation

        // Assert
        Assert.Single(receivedSpeech);
        Assert.Equal("Test message", receivedSpeech[0].Text);
    }

    [Fact]
    public async Task VoiceActuator_GetVoicesAsync_ReturnsVoices()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        var result = await actuator.GetVoicesAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
    }

    [Fact]
    public void VoiceActuator_SetVoice_UpdatesConfig()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        actuator.SetVoice("new-voice-id");

        // Assert
        Assert.Equal("new-voice-id", actuator.Config.Voice);
    }
}
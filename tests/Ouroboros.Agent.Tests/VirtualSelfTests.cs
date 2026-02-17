// <copyright file="VirtualSelfTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.EmbodiedInteraction;

using Ouroboros.Core.EmbodiedInteraction;
using Xunit;

/// <summary>
/// Tests for VirtualSelf and embodiment components.
/// </summary>
[Trait("Category", "Unit")]
public class VirtualSelfTests
{
    [Fact]
    public void VirtualSelf_InitializesWithDefaultState()
    {
        // Arrange & Act
        using var self = new VirtualSelf("test-agent");

        // Assert
        Assert.Equal("test-agent", self.CurrentState.Name);
        Assert.Equal(EmbodimentState.Dormant, self.CurrentState.State);
    }

    [Fact]
    public void VirtualSelf_SetState_UpdatesState()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");

        // Act
        self.SetState(EmbodimentState.Awake);

        // Assert
        Assert.Equal(EmbodimentState.Awake, self.CurrentState.State);
    }

    [Fact]
    public async Task VirtualSelf_PublishTextPerception_EmitsToStream()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var received = new List<PerceptionEvent>();

        using var sub = self.Perceptions.Subscribe(p => received.Add(p));

        // Act
        self.PublishTextPerception("Hello", "user");

        // Small delay for Rx
        await Task.Delay(50);

        // Assert
        Assert.Single(received);
        var textPerception = Assert.IsType<TextPerception>(received[0]);
        Assert.Equal("Hello", textPerception.Text);
    }

    [Fact]
    public async Task VirtualSelf_PublishAudioPerception_EmitsToStream()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var received = new List<PerceptionEvent>();

        using var sub = self.Perceptions.Subscribe(p => received.Add(p));

        // Act
        self.PublishAudioPerception("Testing audio", 0.95, "en-US", TimeSpan.FromSeconds(1));

        // Small delay for Rx
        await Task.Delay(50);

        // Assert
        Assert.Single(received);
        var audioPerception = Assert.IsType<AudioPerception>(received[0]);
        Assert.Equal("Testing audio", audioPerception.TranscribedText);
        Assert.Equal(0.95, audioPerception.Confidence);
    }

    [Fact]
    public async Task VirtualSelf_PublishVisualPerception_EmitsToStream()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var received = new List<PerceptionEvent>();

        using var sub = self.Perceptions.Subscribe(p => received.Add(p));

        // Act
        self.PublishVisualPerception("A person at a desk", confidence: 0.9);

        // Small delay for Rx
        await Task.Delay(50);

        // Assert
        Assert.Single(received);
        var visualPerception = Assert.IsType<VisualPerception>(received[0]);
        Assert.Equal("A person at a desk", visualPerception.Description);
    }

    [Fact]
    public void VirtualSelf_ActivateSensor_UpdatesState()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");

        // Act
        var result = self.ActivateSensor(SensorModality.Audio);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(SensorModality.Audio, self.CurrentState.ActiveSensors);
    }

    [Fact]
    public void VirtualSelf_DeactivateSensor_UpdatesState()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        self.ActivateSensor(SensorModality.Audio);

        // Act
        var result = self.DeactivateSensor(SensorModality.Audio);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(SensorModality.Audio, self.CurrentState.ActiveSensors);
    }

    [Fact]
    public void VirtualSelf_FocusAttention_UpdatesAttention()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");

        // Act
        self.FocusAttention(SensorModality.Visual, "user_face", 0.9);

        // Assert
        Assert.NotNull(self.CurrentState.AttentionFocus);
        Assert.Equal("user_face", self.CurrentState.AttentionFocus.Target);
        Assert.Equal(0.9, self.CurrentState.AttentionFocus.Intensity);
    }

    [Fact]
    public void VirtualSelf_Dispose_CompletesStreams()
    {
        // Arrange
        var self = new VirtualSelf("test-agent");
        var completed = false;

        using var sub = self.Perceptions.Subscribe(
            _ => { },
            () => completed = true);

        // Act
        self.Dispose();

        // Assert
        Assert.True(completed);
    }
}
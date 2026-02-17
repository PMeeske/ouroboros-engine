namespace Ouroboros.Tests.EmbodiedInteraction;

/// <summary>
/// Tests for perception types.
/// </summary>
[Trait("Category", "Unit")]
public class PerceptionTests
{
    [Fact]
    public void TextPerception_CreatesCorrectly()
    {
        // Arrange & Act
        var perception = new TextPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            1.0,
            "Hello, world!",
            "user");

        // Assert
        Assert.Equal("Hello, world!", perception.Text);
        Assert.Equal("user", perception.Source);
        Assert.Equal(SensorModality.Text, perception.Modality);
    }

    [Fact]
    public void AudioPerception_CreatesCorrectly()
    {
        // Arrange & Act
        var perception = new AudioPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            0.92,
            "Testing microphone",
            "en-US",
            null,
            TimeSpan.FromSeconds(2.5),
            true);

        // Assert
        Assert.Equal("Testing microphone", perception.TranscribedText);
        Assert.Equal(TimeSpan.FromSeconds(2.5), perception.Duration);
        Assert.Equal(0.92, perception.Confidence);
        Assert.Equal(SensorModality.Audio, perception.Modality);
    }

    [Fact]
    public void VisualPerception_WithDetections()
    {
        // Arrange
        var objects = new List<DetectedObject>
        {
            new("cup", 0.95, (0.1, 0.2, 0.3, 0.3), null),
            new("laptop", 0.88, (0.5, 0.1, 0.4, 0.4), null)
        };

        var faces = new List<DetectedFace>
        {
            new("face-1", 0.9, (0.3, 0.2, 0.2, 0.2), "Happy", 30, false, null)
        };

        // Act
        var perception = new VisualPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            0.9,
            "A person at a desk with a cup",
            objects,
            faces,
            "office",
            "Happy",
            null);

        // Assert
        Assert.Equal("A person at a desk with a cup", perception.Description);
        Assert.Equal(2, perception.Objects.Count);
        Assert.Single(perception.Faces);
        Assert.Contains("cup", perception.Objects.Select(o => o.Label));
        Assert.Equal(SensorModality.Visual, perception.Modality);
    }

    [Fact]
    public void FusedPerception_CombinesModalities()
    {
        // Arrange
        var audio = new List<AudioPerception>
        {
            new(Guid.NewGuid(), DateTime.UtcNow, 0.9, "Hello", "en", null, TimeSpan.FromSeconds(1), true)
        };
        var visual = new List<VisualPerception>
        {
            new(Guid.NewGuid(), DateTime.UtcNow, 0.8, "Person waving", [], [], null, null, null)
        };
        var text = new List<TextPerception>
        {
            new(Guid.NewGuid(), DateTime.UtcNow, 1.0, "User greeting", "system")
        };

        // Act
        var fused = new FusedPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            audio,
            visual,
            text,
            "[Heard] Hello | [Saw] Person waving | [Read] User greeting",
            0.9);

        // Assert
        Assert.Single(fused.AudioPerceptions);
        Assert.Single(fused.VisualPerceptions);
        Assert.Single(fused.TextPerceptions);
        Assert.True(fused.HasAudio);
        Assert.True(fused.HasVisual);
        Assert.Contains("Hello", fused.CombinedTranscript);
    }
}
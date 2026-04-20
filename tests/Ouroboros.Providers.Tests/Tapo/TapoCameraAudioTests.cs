using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoCameraAudioTests
{
    [Fact]
    public void TapoCameraAudio_Construction_ShouldSetAllProperties()
    {
        // Arrange
        var data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        var duration = TimeSpan.FromSeconds(5);
        var timestamp = new DateTime(2026, 3, 17, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var audio = new TapoCameraAudio(data, 16000, 1, duration, timestamp, "test-camera");

        // Assert
        audio.Data.Should().BeEquivalentTo(data);
        audio.SampleRate.Should().Be(16000);
        audio.Channels.Should().Be(1);
        audio.Duration.Should().Be(duration);
        audio.Timestamp.Should().Be(timestamp);
        audio.CameraName.Should().Be("test-camera");
    }

    [Fact]
    public void TapoCameraAudio_Stereo_ShouldSetChannelsToTwo()
    {
        // Arrange & Act
        var audio = new TapoCameraAudio(
            new byte[] { 0x00 }, 44100, 2, TimeSpan.FromSeconds(1), DateTime.UtcNow, "stereo-cam");

        // Assert
        audio.Channels.Should().Be(2);
        audio.SampleRate.Should().Be(44100);
    }

    [Fact]
    public void TapoCameraAudio_EmptyData_ShouldBeAllowed()
    {
        // Arrange & Act
        var audio = new TapoCameraAudio(
            Array.Empty<byte>(), 16000, 1, TimeSpan.Zero, DateTime.UtcNow, "silent-cam");

        // Assert
        audio.Data.Should().BeEmpty();
        audio.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TapoCameraAudio_With_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new TapoCameraAudio(
            new byte[] { 1, 2, 3 }, 16000, 1, TimeSpan.FromSeconds(1), DateTime.UtcNow, "cam");

        // Act
        var modified = original with { SampleRate = 44100, Channels = 2 };

        // Assert
        modified.SampleRate.Should().Be(44100);
        modified.Channels.Should().Be(2);
        original.SampleRate.Should().Be(16000);
    }
}

using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoCameraConfigTests
{
    [Fact]
    public void TapoCameraConfig_Construction_WithDefaults_ShouldUseDefaultValues()
    {
        // Arrange & Act
        var config = new TapoCameraConfig("living-room-cam");

        // Assert
        config.CameraName.Should().Be("living-room-cam");
        config.StreamQuality.Should().Be(CameraStreamQuality.HD);
        config.EnableAudio.Should().BeTrue();
        config.EnableMotionDetection.Should().BeTrue();
        config.EnablePersonDetection.Should().BeTrue();
        config.FrameRate.Should().Be(15);
        config.VisionModel.Should().Be("llava:13b");
    }

    [Fact]
    public void TapoCameraConfig_Construction_WithCustomValues_ShouldSetProperties()
    {
        // Arrange & Act
        var config = new TapoCameraConfig(
            CameraName: "outdoor-cam",
            StreamQuality: CameraStreamQuality.FullHD,
            EnableAudio: false,
            EnableMotionDetection: false,
            EnablePersonDetection: false,
            FrameRate: 30,
            VisionModel: "llava:34b");

        // Assert
        config.CameraName.Should().Be("outdoor-cam");
        config.StreamQuality.Should().Be(CameraStreamQuality.FullHD);
        config.EnableAudio.Should().BeFalse();
        config.EnableMotionDetection.Should().BeFalse();
        config.EnablePersonDetection.Should().BeFalse();
        config.FrameRate.Should().Be(30);
        config.VisionModel.Should().Be("llava:34b");
    }

    [Fact]
    public void TapoCameraConfig_Equality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var a = new TapoCameraConfig("cam1");
        var b = new TapoCameraConfig("cam1");

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void TapoCameraConfig_Equality_DifferentNames_ShouldNotBeEqual()
    {
        // Arrange
        var a = new TapoCameraConfig("cam1");
        var b = new TapoCameraConfig("cam2");

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void TapoCameraConfig_With_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new TapoCameraConfig("cam1");

        // Act
        var modified = original with { FrameRate = 60 };

        // Assert
        modified.FrameRate.Should().Be(60);
        original.FrameRate.Should().Be(15);
    }
}

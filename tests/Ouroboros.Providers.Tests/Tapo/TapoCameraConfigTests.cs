namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class TapoCameraConfigTests
{
    [Fact]
    public void Ctor_DefaultValues_AreCorrect()
    {
        var config = new TapoCameraConfig("office");

        config.CameraName.Should().Be("office");
        config.StreamQuality.Should().Be(CameraStreamQuality.HD);
        config.EnableAudio.Should().BeTrue();
        config.EnableMotionDetection.Should().BeTrue();
        config.EnablePersonDetection.Should().BeTrue();
        config.FrameRate.Should().Be(15);
        config.VisionModel.Should().Be("llava:13b");
    }

    [Fact]
    public void Ctor_CustomValues_ArePreserved()
    {
        var config = new TapoCameraConfig(
            "garage",
            CameraStreamQuality.FullHD,
            EnableAudio: false,
            EnableMotionDetection: false,
            EnablePersonDetection: false,
            FrameRate: 30,
            VisionModel: "custom-model");

        config.CameraName.Should().Be("garage");
        config.StreamQuality.Should().Be(CameraStreamQuality.FullHD);
        config.EnableAudio.Should().BeFalse();
        config.EnableMotionDetection.Should().BeFalse();
        config.EnablePersonDetection.Should().BeFalse();
        config.FrameRate.Should().Be(30);
        config.VisionModel.Should().Be("custom-model");
    }
}

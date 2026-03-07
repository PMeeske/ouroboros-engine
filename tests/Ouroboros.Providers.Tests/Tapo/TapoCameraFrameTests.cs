namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class TapoCameraFrameTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var data = new byte[] { 0xFF, 0xD8 };
        var ts = DateTime.UtcNow;

        var frame = new TapoCameraFrame(data, 1920, 1080, 42, ts, "cam-office");

        frame.Data.Should().BeSameAs(data);
        frame.Width.Should().Be(1920);
        frame.Height.Should().Be(1080);
        frame.FrameNumber.Should().Be(42);
        frame.Timestamp.Should().Be(ts);
        frame.CameraName.Should().Be("cam-office");
    }
}
